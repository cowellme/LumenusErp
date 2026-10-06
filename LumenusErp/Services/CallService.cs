using LumenusErp.Data;
using Microsoft.EntityFrameworkCore;

namespace LumenusErp.Services;

/// <summary>Строка списка созвонов (без транскрипта, чтобы не тянуть большой текст).</summary>
public record CallSummary(
    Guid Id, string FileName, long SizeBytes, string Status, string Stage, string? Error,
    double? DurationSeconds, DateTime CreatedAt, int SuggestionCount);

/// <summary>Итог «Добавить отмеченные»: сколько задач создано/уже было и ошибки по названиям.</summary>
public record CallAddResult(int Added, int Existing, List<string> Errors);

/// <summary>Операции над записями созвонов пользователя; чужая запись = «не найдена».</summary>
public class CallService(IDbContextFactory<ApplicationDbContext> dbFactory, TaskService tasks)
{
    public static bool IsActive(string status) => status is CallRecording.StatusQueued or CallRecording.StatusProcessing;

    public async Task<List<CallSummary>> ListAsync(string ownerId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.CallRecordings.AsNoTracking()
            .Where(c => c.OwnerId == ownerId)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new CallSummary(c.Id, c.FileName, c.SizeBytes, c.Status, c.Stage, c.Error,
                c.DurationSeconds, c.CreatedAt, c.Suggestions.Count))
            .ToListAsync(ct);
    }

    /// <summary>Запись с подсказками по порядку (с транскриптом) или null.</summary>
    public async Task<CallRecording?> GetAsync(string ownerId, Guid id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var c = await db.CallRecordings.AsNoTracking().Include(x => x.Suggestions)
            .FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == ownerId, ct);
        c?.Suggestions.Sort((a, b) => a.Order.CompareTo(b.Order));
        return c;
    }

    /// <summary>
    /// Физическое удаление записи и подсказок (созданные задачи трекера остаются). Запись в processing не удаляется:
    /// false. Условие проверяется в самом DELETE, чтобы не гоняться с обработчиком.
    /// </summary>
    public async Task<bool> DeleteAsync(string ownerId, Guid id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var n = await db.CallRecordings
            .Where(c => c.Id == id && c.OwnerId == ownerId && c.Status != CallRecording.StatusProcessing)
            .ExecuteDeleteAsync(ct);
        return n > 0;
    }

    /// <summary>
    /// Добавляет в трекер выбранные подсказки (Source = "call", ExternalId = "callId:suggestionId" — повтор не плодит дубли).
    /// <paramref name="picked"/>: Id подсказки → итоговое название (пользователь мог его поправить).
    /// </summary>
    public async Task<CallAddResult> AddToTrackerAsync(string ownerId, Guid callId, IReadOnlyDictionary<Guid, string> picked, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var call = await db.CallRecordings.Include(c => c.Suggestions)
            .FirstOrDefaultAsync(c => c.Id == callId && c.OwnerId == ownerId, ct);
        if (call is null) return new(0, 0, ["Запись не найдена."]);

        int added = 0, existing = 0;
        var errors = new List<string>();
        foreach (var s in call.Suggestions.Where(s => s.TaskItemId is null && picked.ContainsKey(s.Id)).OrderBy(s => s.Order))
        {
            var title = picked[s.Id];
            var sourceText = s.SourceText + "\n\nСозвон: " + call.FileName;
            if (sourceText.Length > TaskService.MaxSourceText) sourceText = sourceText[..TaskService.MaxSourceText];
            var r = await tasks.CreateAsync(ownerId,
                new TaskInput(title, sourceText, TaskService.CallSource, $"{callId}:{s.Id}", null, TaskDates.ToOffset(s.StartAt), TaskDates.ToOffset(s.DueAt)), ct);
            if (r.Item is null)
            {
                errors.Add($"«{title}»: название должно быть 1–{TaskService.MaxTitle} символов.");
                continue;
            }
            s.TaskItemId = r.Item.Id;
            if (r.Created) added++; else existing++;
        }
        await db.SaveChangesAsync(ct);
        return new(added, existing, errors);
    }
}
