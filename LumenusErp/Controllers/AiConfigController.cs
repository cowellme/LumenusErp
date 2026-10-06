using LumenusErp.Services;
using Microsoft.AspNetCore.Mvc;

namespace LumenusErp.Controllers
{
    public record AiConfigDto(string? AsrModel, string? DedupModel, string? DiarizationModel, DateTime? UpdatedAt);

    /// <summary>Модели аудио-этапов и проверки дублей для внешнего сервиса myasi. См. «API задач» в CLAUDE.md.</summary>
    [ApiController]
    [ApiToken]
    [Route("api/ai-config")]
    public class AiConfigController(AiStageModelService stages) : ControllerBase
    {
        [HttpGet]
        public async Task<ActionResult<AiConfigDto>> Get(CancellationToken ct)
        {
            var c = await stages.GetMyasiConfigAsync(ct);
            Response.Headers.ETag = AiConfigRules.ETag(c);
            Response.Headers.CacheControl = "private, no-cache";
            if (AiConfigRules.NotModified(Request.Headers.IfNoneMatch.ToString(), c)) return StatusCode(StatusCodes.Status304NotModified);

            // Из БД DateTime приходит с Kind=Unspecified (legacy timestamp behavior) — помечаем как UTC, чтобы вышло с «Z».
            DateTime? updated = c.UpdatedAt == DateTime.MinValue ? null : DateTime.SpecifyKind(c.UpdatedAt, DateTimeKind.Utc);
            return new AiConfigDto(c.AsrModel, c.DedupModel, c.DiarizationModel, updated);
        }
    }
}
