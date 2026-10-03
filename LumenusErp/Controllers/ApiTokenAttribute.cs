using LumenusErp.Data;
using LumenusErp.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace LumenusErp.Controllers
{
    /// <summary>Владелец запроса к api/tasks, определённый по токену (лежит в HttpContext.Items).</summary>
    public static class ApiOwner
    {
        private const string Key = "ApiOwnerId";

        public static void Set(HttpContext ctx, string userId) => ctx.Items[Key] = userId;

        public static string Get(HttpContext ctx) =>
            ctx.Items[Key] as string ?? throw new InvalidOperationException("Владелец не определён: нет [ApiToken].");
    }

    /// <summary>
    /// Bearer-авторизация: личный токен пользователя (владелец — он) либо, в переходном режиме, общий Api:Token
    /// (владелец — администратор). Нет/неверный токен — 401 без редиректа.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class ApiTokenAttribute : Attribute, IAsyncAuthorizationFilter
    {
        private const string Prefix = "Bearer ";

        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            var http = context.HttpContext;
            string? header = http.Request.Headers.Authorization;
            if (header is not null && header.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
            {
                var token = header.Substring(Prefix.Length).Trim();
                var owner = await ResolveOwnerAsync(http, token);
                if (owner is not null)
                {
                    ApiOwner.Set(http, owner);
                    return;
                }
            }

            http.Response.Headers.WWWAuthenticate = "Bearer";
            context.Result = new UnauthorizedResult();
        }

        private static async Task<string?> ResolveOwnerAsync(HttpContext http, string token)
        {
            var sp = http.RequestServices;
            var personal = await sp.GetRequiredService<UserApiTokenService>().ValidateAsync(token, http.RequestAborted);
            if (personal is not null) return personal;

            // Переходный режим: общий токен пишет задачи администратору.
            if (!MySec.IsValidToken(token)) return null;
            var users = sp.GetRequiredService<UserManager<ApplicationUser>>();
            var email = sp.GetRequiredService<IConfiguration>()["Admin:Email"];
            var admin = string.IsNullOrWhiteSpace(email) ? null : await users.FindByEmailAsync(email);
            admin ??= (await users.GetUsersInRoleAsync("Admin")).OrderBy(u => u.Email, StringComparer.Ordinal).FirstOrDefault();
            return admin?.Id;
        }
    }
}
