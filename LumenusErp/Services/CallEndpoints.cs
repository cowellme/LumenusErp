using System.Security.Claims;
using LumenusErp.Data;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;

namespace LumenusErp.Services;

/// <summary>
/// Загрузка записей созвонов. Пути под /tasks, поэтому на них действует Tasks:Host (на чужом хосте — 404).
/// Страница Blazor Server без пререндера не имеет HttpContext, поэтому antiforgery-токен отдаёт GET-эндпоинт,
/// а загрузка (XMLHttpRequest из calls.js) шлёт его в заголовке.
/// </summary>
public static class CallEndpoints
{
    public const int MaxActivePerUser = 2;

    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".webm", ".mp4", ".mkv", ".mov", ".avi", ".m4a", ".mp3", ".ogg", ".oga", ".opus", ".wav", ".flac", ".aac", ".wma", ".3gp",
    };

    public static void MapCallEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/tasks/calls/antiforgery", (HttpContext ctx, IAntiforgery antiforgery) =>
        {
            var tokens = antiforgery.GetAndStoreTokens(ctx);
            return Results.Json(new { token = tokens.RequestToken, header = tokens.HeaderName });
        }).RequireAuthorization();

        // Форму читаем вручную потоком (MultipartReader), а не через IFormFile: так файл не буферизуется в памяти/на диске
        // дважды и не упирается в FormOptions. Автоматическую antiforgery-проверку отключаем и делаем свою.
        app.MapPost("/tasks/calls/upload", UploadAsync).RequireAuthorization().DisableAntiforgery();
    }

    private static async Task<IResult> UploadAsync(
        HttpContext ctx, IAntiforgery antiforgery, CallSettings settings, CallQueue queue,
        IDbContextFactory<ApplicationDbContext> dbFactory, ILoggerFactory loggers)
    {
        var log = loggers.CreateLogger("LumenusErp.Calls");
        try
        {
            await antiforgery.ValidateRequestAsync(ctx);
        }
        catch (AntiforgeryValidationException)
        {
            return Error(StatusCodes.Status400BadRequest, "Сессия устарела, обновите страницу.");
        }

        var ownerId = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (ownerId is null) return Results.Unauthorized();

        if (!MediaTypeHeaderValue.TryParse(ctx.Request.ContentType, out var mediaType)
            || !mediaType.MediaType.Equals("multipart/form-data", StringComparison.OrdinalIgnoreCase)
            || HeaderUtilities.RemoveQuotes(mediaType.Boundary).Length == 0)
        {
            return Error(StatusCodes.Status415UnsupportedMediaType, "Ожидается multipart/form-data с полем file.");
        }

        // К лимиту добавляем запас на заголовки multipart; лимит снимается только для этого запроса
        const long overhead = 1024 * 1024;
        if (ctx.Request.ContentLength > settings.MaxBytes + overhead) return TooLarge(settings);
        var limit = ctx.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (limit is { IsReadOnly: false }) limit.MaxRequestBodySize = settings.MaxBytes + overhead;

        await using (var db = await dbFactory.CreateDbContextAsync())
        {
            var active = await db.CallRecordings.CountAsync(c =>
                c.OwnerId == ownerId && (c.Status == CallRecording.StatusQueued || c.Status == CallRecording.StatusProcessing));
            if (active >= MaxActivePerUser)
                return Error(StatusCodes.Status429TooManyRequests, $"Одновременно в обработке не больше {MaxActivePerUser} записей: дождитесь завершения.");
        }

        var id = Guid.NewGuid();
        var dir = settings.DirOf(id);
        string? fileName = null;
        long size = 0;
        try
        {
            var reader = new MultipartReader(HeaderUtilities.RemoveQuotes(mediaType.Boundary).Value!, ctx.Request.Body);
            MultipartSection? section;
            while ((section = await reader.ReadNextSectionAsync(ctx.RequestAborted)) is not null)
            {
                if (!ContentDispositionHeaderValue.TryParse(section.ContentDisposition, out var cd)
                    || cd.Name.Value != "file" || (cd.FileName.Value is null && cd.FileNameStar.Value is null))
                {
                    continue;
                }

                fileName = CleanName(cd.FileNameStar.HasValue ? cd.FileNameStar.Value! : cd.FileName.Value!);
                var ext = Path.GetExtension(fileName);
                if (!Extensions.Contains(ext))
                    return Error(StatusCodes.Status415UnsupportedMediaType, "Неподдерживаемый тип файла: нужна видео- или аудиозапись.");

                Directory.CreateDirectory(dir);
                await using var fs = new FileStream(Path.Combine(dir, "input" + ext.ToLowerInvariant()),
                    FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);
                var buffer = new byte[81920];
                int n;
                while ((n = await section.Body.ReadAsync(buffer, ctx.RequestAborted)) > 0)
                {
                    size += n;
                    if (size > settings.MaxBytes)
                    {
                        TryDelete(dir);
                        return TooLarge(settings);
                    }
                    await fs.WriteAsync(buffer.AsMemory(0, n), ctx.RequestAborted);
                }
                break;
            }

            if (fileName is null || size == 0)
            {
                TryDelete(dir);
                return Error(StatusCodes.Status400BadRequest, "Файл не передан или пуст.");
            }

            await using var db = await dbFactory.CreateDbContextAsync();
            var now = DateTime.UtcNow;
            db.CallRecordings.Add(new CallRecording
            {
                Id = id, OwnerId = ownerId, FileName = fileName, SizeBytes = size,
                Status = CallRecording.StatusQueued, Stage = "В очереди", CreatedAt = now, UpdatedAt = now,
            });
            await db.SaveChangesAsync();
            queue.Enqueue(id);
            return Results.Json(new { id }, statusCode: StatusCodes.Status201Created);
        }
        catch (Exception ex) when (ex is BadHttpRequestException or InvalidDataException or IOException or OperationCanceledException)
        {
            // Обрыв загрузки, превышение лимита Kestrel, битый multipart: временные файлы не оставляем
            TryDelete(dir);
            log.LogInformation(ex, "Загрузка созвона прервана");
            return Error(StatusCodes.Status400BadRequest, "Загрузка прервана или повреждена.");
        }
        catch
        {
            TryDelete(dir);
            throw;
        }
    }

    private static IResult TooLarge(CallSettings s) =>
        Error(StatusCodes.Status413PayloadTooLarge, $"Файл больше {s.MaxBytes / (1024 * 1024)} МБ.");

    private static IResult Error(int status, string message) => Results.Json(new { error = message }, statusCode: status);

    /// <summary>Имя без пути и управляющих символов, ≤255 (расширение сохраняется).</summary>
    private static string CleanName(string name)
    {
        var n = new string(Path.GetFileName(name.Replace('\\', '/')).Where(c => !char.IsControl(c)).ToArray()).Trim();
        if (n.Length <= 255) return n;
        var ext = Path.GetExtension(n);
        return n[..(255 - ext.Length)] + ext;
    }

    private static void TryDelete(string dir)
    {
        try { if (Directory.Exists(dir)) Directory.Delete(dir, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}
