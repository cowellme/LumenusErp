using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace LumenusErp.Controllers
{
    /// <summary>
    /// Bearer-авторизация по Api:Token. Нет/неверный токен (или пустой Api:Token) — 401 без редиректа.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class ApiTokenAttribute : Attribute, IAuthorizationFilter
    {
        private const string Prefix = "Bearer ";

        public void OnAuthorization(AuthorizationFilterContext context)
        {
            string? header = context.HttpContext.Request.Headers.Authorization;
            if (header is not null && header.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
            {
                var token = header.Substring(Prefix.Length).Trim();
                if (MySec.IsValidToken(token))
                {
                    return;
                }
            }

            context.HttpContext.Response.Headers.WWWAuthenticate = "Bearer";
            context.Result = new UnauthorizedResult();
        }
    }
}
