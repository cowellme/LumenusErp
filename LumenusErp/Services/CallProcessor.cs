using System.ComponentModel;
using System.Diagnostics;
using LumenusErp.Data;
using Microsoft.EntityFrameworkCore;

namespace LumenusErp.Services;

/// <summary>
/// Фоновая обработка записей созвонов, по одной за раз: ffmpeg (куски WAV по 10 минут) → myasi (текст) → LLM (задачи).
/// Очередь живёт в памяти, поэтому при старте незавершённые записи прошлого запуска помечаются failed.
/// Временный каталог записи удаляется всегда; исключения не роняют сервис.
/// </summary>
public class CallProcessor(
    CallQueue queue, CallSettings settings, IHttpClientFactory httpFactory,
    IDbContextFactory<ApplicationDbContext> dbFactory, UserPromptService prompts, TaskTimeZone taskTz, ILogger<CallProcessor> log) : BackgroundService
{
    public const string HttpClientName = "myasi";
    public const string InterruptedError = "Обработка прервана перезапуском сервера";

    private const int SegmentSeconds = 600;
    private const int MaxTranscriptForLlm = 120_000;
    private const int MaxErrorLength = 500;
    private const int WavHeaderBytes = 44;
    private const long WavBytesPerSecond = 16000 * 2;
    private static readonly TimeSpan FfmpegTimeout = TimeSpan.FromMinutes(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RecoverAsync();
        try
        {
            await foreach (var id in queue.ReadAllAsync(stoppingToken))
            {
                await ProcessSafeAsync(id, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    /// <summary>Записи queued/processing остались от прошлого запуска: очередь потеряна, файлы чистим.</summary>
    private async Task RecoverAsync()
    {
        try
        {
            await using var db = await dbFactory.CreateDbContextAsync();
            var n = await db.CallRecordings
                .Where(c => c.Status == CallRecording.StatusQueued || c.Status == CallRecording.StatusProcessing)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(c => c.Status, CallRecording.StatusFailed)
                    .SetProperty(c => c.Stage, "")
                    .SetProperty(c => c.Error, InterruptedError)
                    .SetProperty(c => c.UpdatedAt, DateTime.UtcNow));
            if (n > 0) log.LogWarning("Созвоны: {Count} незавершённых записей помечены failed после перезапуска", n);

            foreach (var dir in Directory.GetDirectories(settings.WorkRoot)) TryDeleteDir(dir);
            foreach (var file in Directory.GetFiles(settings.WorkRoot)) File.Delete(file);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Созвоны: не удалось восстановиться после перезапуска");
        }
    }

    private async Task ProcessSafeAsync(Guid id, CancellationToken ct)
    {
        var dir = settings.DirOf(id);
        try
        {
            await ProcessAsync(id, dir, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            await TryFailAsync(id, InterruptedError);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Созвоны: ошибка обработки записи {Id}", id);
            await TryFailAsync(id, "Внутренняя ошибка обработки");
        }
        finally
        {
            TryDeleteDir(dir);
        }
    }

    private async Task ProcessAsync(Guid id, string dir, CancellationToken ct)
    {
        // Атомарно забираем только queued: запись могла быть удалена пользователем, пока ждала в очереди
        await using (var db = await dbFactory.CreateDbContextAsync(ct))
        {
            var claimed = await db.CallRecordings
                .Where(c => c.Id == id && c.Status == CallRecording.StatusQueued)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(c => c.Status, CallRecording.StatusProcessing)
                    .SetProperty(c => c.Stage, "Извлечение аудио")
                    .SetProperty(c => c.UpdatedAt, DateTime.UtcNow), ct);
            if (claimed == 0) return;
        }

        // 1. ffmpeg
        var input = Directory.Exists(dir) ? Directory.GetFiles(dir, "input.*").FirstOrDefault() : null;
        if (input is null)
        {
            await FailAsync(id, "Загруженный файл не найден");
            return;
        }
        var ff = await ConvertAsync(input, dir, ct);
        File.Delete(input);
        if (ff.Error is not null)
        {
            await FailAsync(id, ff.Error);
            return;
        }
        var parts = ff.Parts;
        var duration = parts.Sum(p => (new FileInfo(p).Length - WavHeaderBytes) / (double)WavBytesPerSecond);

        // 2. myasi
        var myasi = new MyasiClient(httpFactory.CreateClient(HttpClientName));
        var texts = new List<string>();
        for (var i = 0; i < parts.Count; i++)
        {
            await UpdateAsync(id, c => c.Stage = $"Распознавание {i + 1}/{parts.Count}");
            try
            {
                var text = (await myasi.TranscribeAsync(parts[i], ct)).Trim();
                if (text.Length > 0) texts.Add(text);
            }
            catch (MyasiException ex)
            {
                log.LogWarning("Созвоны: myasi, запись {Id}, кусок {Part}: {Message}", id, i + 1, ex.Message);
                await FailAsync(id, ex.Message);
                return;
            }
        }
        var transcript = string.Join(" ", texts);

        // Транскрипт сохраняем сразу: он виден, даже если выделение задач упадёт
        await UpdateAsync(id, c =>
        {
            c.Transcript = transcript;
            c.DurationSeconds = duration;
            c.Stage = transcript.Length == 0 ? "Готово" : "Выделение задач";
        });
        if (transcript.Length == 0)
        {
            await UpdateAsync(id, c => c.Status = CallRecording.StatusDone);
            return;
        }

        // 3. LLM
        var forLlm = transcript;
        if (forLlm.Length > MaxTranscriptForLlm)
        {
            log.LogWarning("Созвоны: транскрипт записи {Id} ({Length} симв.) обрезан до {Max} для модели", id, forLlm.Length, MaxTranscriptForLlm);
            forLlm = forLlm[..MaxTranscriptForLlm];
        }
        string raw;
        ResolvedPrompt prompt;
        try
        {
            // Промпт владельца записи: личный перекрывает общий
            string ownerId;
            DateTime uploadedAt;
            await using (var db = await dbFactory.CreateDbContextAsync(ct))
            {
                var rec = await db.CallRecordings.Where(x => x.Id == id).Select(x => new { x.OwnerId, x.CreatedAt }).FirstOrDefaultAsync(ct);
                ownerId = rec?.OwnerId ?? "";
                uploadedAt = rec?.CreatedAt ?? DateTime.UtcNow;
            }
            prompt = await prompts.GetForUserAsync(ownerId, DefaultPrompts.CallTasksKey, ct);
            // Первая строка — «сейчас» на момент загрузки записи: обсуждение было тогда, от этой даты считаются «до пятницы» и т. п.
            var message = TaskTimeZone.DateContext(uploadedAt, taskTz.Info) + "\n\n" + forLlm;
            raw = await AiModule.ExtractCallTasksAsync(message, new AiPromptSettings(prompt.Text, prompt.Model, prompt.Temperature));
        }
        catch (AiNotConfiguredException)
        {
            await UpdateAsync(id, c =>
            {
                c.Status = CallRecording.StatusDone;
                c.Stage = "Готово";
                c.Error = "ИИ не настроен — задачи не выделены";
            });
            return;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            log.LogWarning(ex, "Созвоны: ошибка LLM, запись {Id}", id);
            await FailAsync(id, "Не удалось выделить задачи: " + Trim(ex.Message));
            return;
        }

        var tasks = CallTaskParser.Parse(raw, taskTz.Info);
        if (tasks is null)
        {
            log.LogWarning("Созвоны: не разобран ответ модели, запись {Id}: {Raw}", id, Trim(raw));
            await FailAsync(id, "Не удалось разобрать ответ модели");
            return;
        }

        await using (var db = await dbFactory.CreateDbContextAsync(ct))
        {
            var c = await db.CallRecordings.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (c is null) return;
            for (var i = 0; i < tasks.Count; i++)
            {
                db.CallTaskSuggestions.Add(new CallTaskSuggestion
                {
                    Id = Guid.NewGuid(), CallRecordingId = id, Order = i, Title = tasks[i].Title, SourceText = tasks[i].Quote,
                    StartAt = tasks[i].StartAt, DueAt = tasks[i].DueAt,
                });
            }
            c.Status = CallRecording.StatusDone;
            c.Stage = "Готово";
            c.PromptSource = prompt.Source;
            c.PromptUpdatedAt = prompt.UpdatedAt == DateTime.MinValue ? null : prompt.UpdatedAt;
            c.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }
    }

    private record ConvertResult(List<string> Parts, string? Error);

    /// <summary>Извлекает аудио в WAV 16 кГц моно s16 кусками по 10 минут (лимит myasi ~60 МБ на запрос).</summary>
    private async Task<ConvertResult> ConvertAsync(string input, string dir, CancellationToken ct)
    {
        var psi = new ProcessStartInfo("ffmpeg")
        {
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var a in new[]
        {
            "-nostdin", "-hide_banner", "-loglevel", "error", "-i", input, "-vn", "-ac", "1", "-ar", "16000",
            "-sample_fmt", "s16", "-f", "segment", "-segment_time", SegmentSeconds.ToString(), "-c:a", "pcm_s16le",
            Path.Combine(dir, "part%03d.wav"),
        })
        {
            psi.ArgumentList.Add(a);
        }

        string stderr;
        int exitCode;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(FfmpegTimeout);
        Process proc;
        try
        {
            proc = Process.Start(psi) ?? throw new InvalidOperationException("ffmpeg не запустился");
        }
        catch (Win32Exception ex)
        {
            log.LogError(ex, "Созвоны: ffmpeg не найден");
            return new([], "На сервере не установлен ffmpeg");
        }
        using (proc)
        {
            var errTask = proc.StandardError.ReadToEndAsync(CancellationToken.None);
            try
            {
                await proc.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                try { proc.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
                if (ct.IsCancellationRequested) throw;
                return new([], "Конвертация аудио заняла слишком много времени");
            }
            stderr = await errTask;
            exitCode = proc.ExitCode;
        }

        if (exitCode != 0)
        {
            log.LogWarning("Созвоны: ffmpeg завершился с кодом {Code}: {Stderr}", exitCode, Trim(stderr));
            return new([], stderr.Contains("does not contain any stream", StringComparison.OrdinalIgnoreCase)
                ? "В файле нет аудиодорожки"
                : "Не удалось прочитать файл: " + Trim(stderr));
        }

        var parts = Directory.GetFiles(dir, "part*.wav").OrderBy(p => p, StringComparer.Ordinal)
            .Where(p => new FileInfo(p).Length > WavHeaderBytes).ToList();
        return parts.Count == 0 ? new([], "В файле нет аудиодорожки") : new(parts, null);
    }

    private Task FailAsync(Guid id, string error) =>
        UpdateAsync(id, c =>
        {
            c.Status = CallRecording.StatusFailed;
            c.Stage = "";
            c.Error = Trim(error, 2000);
        });

    private async Task TryFailAsync(Guid id, string error)
    {
        try { await FailAsync(id, error); }
        catch (Exception ex) { log.LogError(ex, "Созвоны: не удалось пометить запись {Id} как failed", id); }
    }

    private async Task UpdateAsync(Guid id, Action<CallRecording> apply)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var c = await db.CallRecordings.FirstOrDefaultAsync(x => x.Id == id);
        if (c is null) return;
        apply(c);
        c.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    private static string Trim(string s, int max = MaxErrorLength)
    {
        s = s.Trim();
        return s.Length > max ? s[..max] + "…" : s;
    }

    private void TryDeleteDir(string dir)
    {
        try
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Созвоны: не удалось удалить {Dir}", dir);
        }
    }
}
