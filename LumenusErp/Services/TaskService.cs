using LumenusErp.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace LumenusErp.Services;

/// <summary>Результат создания задачи: Created=false — вернулась существующая (идемпотентность); Errors — ошибки валидации по полям.</summary>
public record TaskCreateResult(TaskItem? Item, bool Created, Dictionary<string, string[]>? Errors = null);

/// <summary>Ввод создания задачи (поля как в api/tasks).</summary>
public record TaskInput(string? Title, string? SourceText, string? Source, string? ExternalId, DateTimeOffset? CreatedAt);

/// <summary>
/// Операции над задачами пользователя; общий код для api/tasks и страницы /tasks.
/// Все методы работают только с задачами владельца: чужая задача = «не найдена».
/// </summary>
public class TaskService(IDbContextFactory<ApplicationDbContext> dbFactory)
{
    public const int MaxTitle = 300, MaxSourceText = 20000, MaxSource = 50, MaxExternalId = 100;
    public const string WebSource = "web";

    public static bool IsStatus(string? s) => s is TaskItem.StatusOpen or TaskItem.StatusDone;

    /// <summary>Название после Trim или null, если не влезает в 1–300.</summary>
    public static string? NormalizeTitle(string? title)
    {
        var t = (title ?? "").Trim();
        return t.Length is 0 or > MaxTitle ? null : t;
    }

    public async Task<List<TaskItem>> ListAsync(string ownerId, string? source = null, string? status = null, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var q = db.TaskItems.AsNoTracking().Where(t => t.OwnerId == ownerId && t.DeletedAt == null);
        if (source is not null) q = q.Where(t => t.Source == source);
        if (status is not null) q = q.Where(t => t.Status == status);
        return await q.OrderByDescending(t => t.CreatedAt).ThenBy(t => t.Id).ToListAsync(ct);
    }

    public async Task<TaskItem?> GetAsync(string ownerId, Guid id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.TaskItems.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id && t.OwnerId == ownerId && t.DeletedAt == null, ct);
    }

    public async Task<TaskCreateResult> CreateAsync(string ownerId, TaskInput req, CancellationToken ct = default)
    {
        var title = (req.Title ?? "").Trim();
        var source = (req.Source ?? "").Trim();
        var sourceText = req.SourceText ?? "";
        var externalId = string.IsNullOrEmpty(req.ExternalId) ? null : req.ExternalId;

        var errors = new Dictionary<string, string[]>();
        if (title.Length is 0 or > MaxTitle) errors["title"] = [$"Обязательно, 1–{MaxTitle} символов."];
        if (source.Length is 0 or > MaxSource) errors["source"] = [$"Обязательно, 1–{MaxSource} символов."];
        if (sourceText.Length > MaxSourceText) errors["sourceText"] = [$"Не более {MaxSourceText} символов."];
        if (externalId is { Length: > MaxExternalId }) errors["externalId"] = [$"Не более {MaxExternalId} символов."];
        if (errors.Count > 0) return new(null, false, errors);

        await using var db = await dbFactory.CreateDbContextAsync(ct);

        if (externalId is not null)
        {
            var same = await FindByExternalId(db, ownerId, source, externalId, ct);
            if (same is not null) return new(same, false);
        }

        var normalized = title.ToLowerInvariant();
        var dup = await db.TaskItems.AsNoTracking().FirstOrDefaultAsync(t =>
            t.OwnerId == ownerId && t.DeletedAt == null && t.Source == source && t.Status == TaskItem.StatusOpen && t.TitleNormalized == normalized, ct);
        if (dup is not null) return new(dup, false);

        var created = req.CreatedAt?.UtcDateTime ?? DateTime.UtcNow;
        var item = new TaskItem
        {
            Id = Guid.NewGuid(),
            OwnerId = ownerId,
            Title = title,
            TitleNormalized = normalized,
            Status = TaskItem.StatusOpen,
            SourceText = sourceText,
            Source = source,
            ExternalId = externalId,
            CreatedAt = created,
            UpdatedAt = created,
        };
        db.TaskItems.Add(item);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (externalId is not null && ex.InnerException is PostgresException { SqlState: "23505" })
        {
            db.ChangeTracker.Clear();
            var existing = await FindByExternalId(db, ownerId, source, externalId, ct);
            if (existing is null) throw;
            return new(existing, false);
        }
        return new(item, true);
    }

    /// <summary>Задача, созданная вручную в трекере (Source = "web").</summary>
    public Task<TaskCreateResult> CreateManualAsync(string ownerId, string? title, CancellationToken ct = default) =>
        CreateAsync(ownerId, new TaskInput(title, null, WebSource, null, null), ct);

    public async Task<TaskItem?> SetStatusAsync(string ownerId, Guid id, string status, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var t = await db.TaskItems.FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == ownerId && x.DeletedAt == null, ct);
        if (t is null) return null;
        t.Status = status;
        t.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return t;
    }

    /// <summary>Переименование. null — задачи нет; невалидное название — ArgumentException.</summary>
    public async Task<TaskItem?> RenameAsync(string ownerId, Guid id, string? title, CancellationToken ct = default)
    {
        var clean = NormalizeTitle(title) ?? throw new ArgumentException($"Название: 1–{MaxTitle} символов.", nameof(title));
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var t = await db.TaskItems.FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == ownerId && x.DeletedAt == null, ct);
        if (t is null) return null;
        t.Title = clean;
        t.TitleNormalized = clean.ToLowerInvariant();
        t.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return t;
    }

    /// <summary>Мягкое удаление. false — задачи нет.</summary>
    public async Task<bool> DeleteAsync(string ownerId, Guid id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var t = await db.TaskItems.FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == ownerId && x.DeletedAt == null, ct);
        if (t is null) return false;
        t.DeletedAt = t.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return true;
    }

    private static Task<TaskItem?> FindByExternalId(ApplicationDbContext db, string ownerId, string source, string externalId, CancellationToken ct) =>
        db.TaskItems.AsNoTracking().FirstOrDefaultAsync(t => t.OwnerId == ownerId && t.Source == source && t.ExternalId == externalId, ct);
}
