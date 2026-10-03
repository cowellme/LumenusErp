using Microsoft.AspNetCore.Mvc;

namespace LumenusErp.Controllers
{
    public record MeDto(string UserId);

    /// <summary>
    /// Владелец Bearer-токена: стабильный Id пользователя (одинаков для всех его токенов, в т.ч. после перевыпуска).
    /// Нужен myasi, чтобы отправлять задачи из очереди токеном того же пользователя. См. «API задач» в CLAUDE.md.
    /// </summary>
    [ApiController]
    [ApiToken]
    [Route("api/me")]
    public class MeController : ControllerBase
    {
        [HttpGet]
        public ActionResult<MeDto> Get() => new MeDto(ApiOwner.Get(HttpContext));
    }
}
