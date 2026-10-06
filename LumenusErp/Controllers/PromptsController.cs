using LumenusErp.Services;
using Microsoft.AspNetCore.Mvc;

namespace LumenusErp.Controllers
{
    public record PromptDto(string Key, string Text, string? Model, double? Temperature, DateTime? UpdatedAt, string Source);

    /// <summary>Промпты для внешнего сервиса myasi. Наружу отдаётся только myasi-tasks. См. «API задач» в CLAUDE.md.</summary>
    [ApiController]
    [ApiToken]
    [Route("api/prompts")]
    public class PromptsController(UserPromptService prompts) : ControllerBase
    {
        [HttpGet("{key}")]
        public async Task<ActionResult<PromptDto>> Get(string key, CancellationToken ct)
        {
            if (key != DefaultPrompts.MyasiTasksKey) return NotFound();

            var p = await prompts.GetForUserAsync(ApiOwner.Get(HttpContext), key, ct);
            var etag = UserPromptRules.ETag(p);
            Response.Headers.ETag = etag;
            Response.Headers.CacheControl = "private, no-cache";
            if (UserPromptRules.NotModified(Request.Headers.IfNoneMatch.ToString(), etag)) return StatusCode(StatusCodes.Status304NotModified);

            // Из БД DateTime приходит с Kind=Unspecified (legacy timestamp behavior) — помечаем как UTC, чтобы вышло с «Z».
            DateTime? updated = p.UpdatedAt == DateTime.MinValue ? null : DateTime.SpecifyKind(p.UpdatedAt, DateTimeKind.Utc);
            return new PromptDto(p.Key, p.Text, p.Model, p.Temperature, updated, p.Source);
        }
    }
}
