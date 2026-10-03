using LumenusErp.Data;
using LumenusErp.Services;
using Microsoft.AspNetCore.Mvc;

namespace LumenusErp.Controllers
{
    public record CreateTaskRequest(string? Title, string? SourceText, string? Source, string? ExternalId, DateTimeOffset? CreatedAt);

    public record UpdateTaskRequest(string? Status);

    public record TaskDto(Guid Id, string Title, string Status, string SourceText, string Source, string? ExternalId, DateTime CreatedAt, DateTime UpdatedAt);

    /// <summary>API задач для внешнего сервиса myasi. См. раздел «API задач» в CLAUDE.md.</summary>
    [ApiController]
    [ApiToken]
    [Route("api/tasks")]
    public class TasksController(TaskService tasks) : ControllerBase
    {
        private string Owner => ApiOwner.Get(HttpContext);

        [HttpGet]
        public async Task<ActionResult<List<TaskDto>>> List(string? source, string? status, CancellationToken ct)
        {
            if (status is not null && !TaskService.IsStatus(status))
            {
                return ValidationError("status", "Допустимо: open, done.");
            }

            var items = await tasks.ListAsync(Owner, source, status, ct);
            return items.Select(ToDto).ToList();
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<TaskDto>> Get(Guid id, CancellationToken ct)
        {
            var t = await tasks.GetAsync(Owner, id, ct);
            return t is null ? NotFound() : ToDto(t);
        }

        [HttpPost]
        public async Task<ActionResult<TaskDto>> Create(CreateTaskRequest req, CancellationToken ct)
        {
            var r = await tasks.CreateAsync(Owner, new TaskInput(req.Title, req.SourceText, req.Source, req.ExternalId, req.CreatedAt), ct);
            if (r.Errors is not null) return ValidationProblem(new ValidationProblemDetails(r.Errors));
            return r.Created ? Created($"/api/tasks/{r.Item!.Id}", ToDto(r.Item)) : Ok(ToDto(r.Item!));
        }

        [HttpPatch("{id:guid}")]
        public async Task<ActionResult<TaskDto>> Update(Guid id, UpdateTaskRequest req, CancellationToken ct)
        {
            if (!TaskService.IsStatus(req.Status))
            {
                return ValidationError("status", "Допустимо: open, done.");
            }

            var t = await tasks.SetStatusAsync(Owner, id, req.Status!, ct);
            return t is null ? NotFound() : ToDto(t);
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
            await tasks.DeleteAsync(Owner, id, ct) ? NoContent() : NotFound();

        private ActionResult ValidationError(string field, string message) =>
            ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]> { [field] = [message] }));

        // Из БД DateTime приходит с Kind=Unspecified (legacy timestamp behavior) — помечаем как UTC, чтобы вышло с «Z».
        private static DateTime Utc(DateTime d) => DateTime.SpecifyKind(d, DateTimeKind.Utc);

        private static TaskDto ToDto(TaskItem t) =>
            new(t.Id, t.Title, t.Status, t.SourceText, t.Source, t.ExternalId, Utc(t.CreatedAt), Utc(t.UpdatedAt));
    }
}
