using LumenusErp.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace LumenusErp.Controllers
{
    public record CreateTaskRequest(string? Title, string? SourceText, string? Source, string? ExternalId, DateTimeOffset? CreatedAt);

    public record UpdateTaskRequest(string? Status);

    public record TaskDto(Guid Id, string Title, string Status, string SourceText, string Source, string? ExternalId, DateTime CreatedAt, DateTime UpdatedAt);

    /// <summary>API задач для внешнего сервиса myasi. См. раздел «API задач» в CLAUDE.md.</summary>
    [ApiController]
    [ApiToken]
    [Route("api/tasks")]
    public class TasksController(IDbContextFactory<ApplicationDbContext> dbFactory) : ControllerBase
    {
        private const int MaxTitle = 300, MaxSourceText = 20000, MaxSource = 50, MaxExternalId = 100;

        [HttpGet]
        public async Task<ActionResult<List<TaskDto>>> List(string? source, string? status, CancellationToken ct)
        {
            if (status is not null && !IsStatus(status))
            {
                return ValidationError("status", "Допустимо: open, done.");
            }

            await using var db = await dbFactory.CreateDbContextAsync(ct);
            var q = db.TaskItems.AsNoTracking().Where(t => t.DeletedAt == null);
            if (source is not null) q = q.Where(t => t.Source == source);
            if (status is not null) q = q.Where(t => t.Status == status);
            var items = await q.OrderByDescending(t => t.CreatedAt).ThenBy(t => t.Id).ToListAsync(ct);
            return items.Select(ToDto).ToList();
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<TaskDto>> Get(Guid id, CancellationToken ct)
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);
            var t = await db.TaskItems.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.DeletedAt == null, ct);
            return t is null ? NotFound() : ToDto(t);
        }

        [HttpPost]
        public async Task<ActionResult<TaskDto>> Create(CreateTaskRequest req, CancellationToken ct)
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
            if (errors.Count > 0) return ValidationProblem(new ValidationProblemDetails(errors));

            await using var db = await dbFactory.CreateDbContextAsync(ct);

            if (externalId is not null)
            {
                var same = await FindByExternalId(db, source, externalId, ct);
                if (same is not null) return Ok(ToDto(same));
            }

            var normalized = title.ToLowerInvariant();
            var dup = await db.TaskItems.AsNoTracking().FirstOrDefaultAsync(t =>
                t.DeletedAt == null && t.Source == source && t.Status == TaskItem.StatusOpen && t.TitleNormalized == normalized, ct);
            if (dup is not null) return Ok(ToDto(dup));

            var created = req.CreatedAt?.UtcDateTime ?? DateTime.UtcNow;
            var item = new TaskItem
            {
                Id = Guid.NewGuid(),
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
                var existing = await FindByExternalId(db, source, externalId, ct);
                if (existing is null) throw;
                return Ok(ToDto(existing));
            }

            return Created($"/api/tasks/{item.Id}", ToDto(item));
        }

        [HttpPatch("{id:guid}")]
        public async Task<ActionResult<TaskDto>> Update(Guid id, UpdateTaskRequest req, CancellationToken ct)
        {
            if (req.Status is null || !IsStatus(req.Status))
            {
                return ValidationError("status", "Допустимо: open, done.");
            }

            await using var db = await dbFactory.CreateDbContextAsync(ct);
            var t = await db.TaskItems.FirstOrDefaultAsync(x => x.Id == id && x.DeletedAt == null, ct);
            if (t is null) return NotFound();
            t.Status = req.Status;
            t.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            return ToDto(t);
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);
            var t = await db.TaskItems.FirstOrDefaultAsync(x => x.Id == id && x.DeletedAt == null, ct);
            if (t is null) return NotFound();
            t.DeletedAt = t.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            return NoContent();
        }

        private static bool IsStatus(string s) => s is TaskItem.StatusOpen or TaskItem.StatusDone;

        private static Task<TaskItem?> FindByExternalId(ApplicationDbContext db, string source, string externalId, CancellationToken ct) =>
            db.TaskItems.AsNoTracking().FirstOrDefaultAsync(t => t.Source == source && t.ExternalId == externalId, ct);

        private ActionResult ValidationError(string field, string message) =>
            ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]> { [field] = [message] }));

        // Из БД DateTime приходит с Kind=Unspecified (legacy timestamp behavior) — помечаем как UTC, чтобы вышло с «Z».
        private static DateTime Utc(DateTime d) => DateTime.SpecifyKind(d, DateTimeKind.Utc);

        private static TaskDto ToDto(TaskItem t) =>
            new(t.Id, t.Title, t.Status, t.SourceText, t.Source, t.ExternalId, Utc(t.CreatedAt), Utc(t.UpdatedAt));
    }
}
