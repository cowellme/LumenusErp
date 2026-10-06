using LumenusErp.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace LumenusErp.Services;

public sealed record AdminResult(bool Ok, string? Error = null)
{
    public static AdminResult Success { get; } = new(true);
    public static AdminResult Fail(string error) => new(false, error);
}

public sealed record AdminUserRow(string Id, string Email, bool EmailConfirmed, bool IsLockedOut, List<string> Roles);

public sealed record AdminRoleRow(string Name, int UserCount, bool IsSystem);

/// <summary>Управление пользователями и ролями для админки. Ожидаемые ошибки возвращаются в <see cref="AdminResult"/>.</summary>
public sealed class AdminUserService(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager)
{
    /// <summary>Системные роли: сидятся при старте, удалить нельзя.</summary>
    public static readonly string[] SystemRoles = ["Admin", "Manager", "User", "Ghost", "Aos", ContentPageAccess.CreatorRole];

    // Identity сравнивает имена ролей по нормализованному виду, поэтому и здесь без учёта регистра.
    public static bool IsSystemRole(string name) => SystemRoles.Contains(name, StringComparer.OrdinalIgnoreCase);

    public async Task<List<AdminUserRow>> ListUsersAsync()
    {
        var users = await userManager.Users.OrderBy(u => u.Email).ToListAsync();
        var rows = new List<AdminUserRow>();
        // Пользователей мало, поэтому роли берём по одному.
        foreach (var u in users)
        {
            var roles = await userManager.GetRolesAsync(u);
            rows.Add(new AdminUserRow(
                u.Id,
                u.Email ?? "",
                u.EmailConfirmed,
                u.LockoutEnd.HasValue && u.LockoutEnd.Value > DateTimeOffset.UtcNow,
                roles.OrderBy(r => r).ToList()));
        }
        return rows;
    }

    public async Task<List<AdminRoleRow>> ListRolesAsync()
    {
        var names = await roleManager.Roles.Select(r => r.Name!).OrderBy(n => n).ToListAsync();
        var rows = new List<AdminRoleRow>();
        foreach (var name in names)
        {
            var count = (await userManager.GetUsersInRoleAsync(name)).Count;
            rows.Add(new AdminRoleRow(name, count, IsSystemRole(name)));
        }
        return rows;
    }

    public async Task<AdminResult> CreateUserAsync(string? email, string? password, IEnumerable<string> roles)
    {
        email = email?.Trim();
        if (string.IsNullOrEmpty(email)) return AdminResult.Fail("Укажите e-mail");
        if (string.IsNullOrEmpty(password)) return AdminResult.Fail("Укажите пароль");

        if (await userManager.FindByEmailAsync(email) != null)
            return AdminResult.Fail("Пользователь с таким e-mail уже есть");

        var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true };
        var create = await userManager.CreateAsync(user, password);
        if (!create.Succeeded) return AdminResult.Fail(Describe(create));

        var existing = new List<string>();
        foreach (var role in roles.Distinct())
        {
            if (await roleManager.RoleExistsAsync(role)) existing.Add(role);
        }
        if (existing.Count > 0)
        {
            var add = await userManager.AddToRolesAsync(user, existing);
            if (!add.Succeeded)
                return AdminResult.Fail($"Пользователь создан, но роли не назначены: {Describe(add)}");
        }
        return AdminResult.Success;
    }

    public async Task<AdminResult> AddRoleAsync(string userId, string role)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user == null) return AdminResult.Fail("Пользователь не найден");
        if (!await roleManager.RoleExistsAsync(role)) return AdminResult.Fail($"Роли «{role}» нет");
        if (await userManager.IsInRoleAsync(user, role)) return AdminResult.Success;

        var result = await userManager.AddToRoleAsync(user, role);
        return result.Succeeded ? AdminResult.Success : AdminResult.Fail(Describe(result));
    }

    public async Task<AdminResult> RemoveRoleAsync(string userId, string role, string? currentUserId)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user == null) return AdminResult.Fail("Пользователь не найден");
        if (!await userManager.IsInRoleAsync(user, role)) return AdminResult.Success;

        if (string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            if (userId == currentUserId)
                return AdminResult.Fail("Нельзя снять роль Admin с самого себя");
            if ((await userManager.GetUsersInRoleAsync("Admin")).Count <= 1)
                return AdminResult.Fail("Нельзя снять роль Admin с последнего администратора");
        }

        var result = await userManager.RemoveFromRoleAsync(user, role);
        return result.Succeeded ? AdminResult.Success : AdminResult.Fail(Describe(result));
    }

    public async Task<AdminResult> CreateRoleAsync(string? name)
    {
        name = name?.Trim();
        if (string.IsNullOrEmpty(name)) return AdminResult.Fail("Укажите название роли");
        if (name.Length > 64) return AdminResult.Fail("Название роли не длиннее 64 символов");
        if (await roleManager.RoleExistsAsync(name)) return AdminResult.Fail("Такая роль уже есть");

        var result = await roleManager.CreateAsync(new IdentityRole(name));
        return result.Succeeded ? AdminResult.Success : AdminResult.Fail(Describe(result));
    }

    public async Task<AdminResult> DeleteRoleAsync(string name)
    {
        if (IsSystemRole(name)) return AdminResult.Fail("Системную роль удалить нельзя");
        var role = await roleManager.FindByNameAsync(name);
        if (role == null) return AdminResult.Fail("Роль не найдена");

        var count = (await userManager.GetUsersInRoleAsync(name)).Count;
        if (count > 0) return AdminResult.Fail($"Роль «{name}» назначена пользователям: {count}");

        var result = await roleManager.DeleteAsync(role);
        return result.Succeeded ? AdminResult.Success : AdminResult.Fail(Describe(result));
    }

    public async Task<AdminResult> ResetPasswordAsync(string userId, string? newPassword)
    {
        if (string.IsNullOrEmpty(newPassword)) return AdminResult.Fail("Укажите новый пароль");
        var user = await userManager.FindByIdAsync(userId);
        if (user == null) return AdminResult.Fail("Пользователь не найден");

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var result = await userManager.ResetPasswordAsync(user, token, newPassword);
        return result.Succeeded ? AdminResult.Success : AdminResult.Fail(Describe(result));
    }

    private static string Describe(IdentityResult result) =>
        string.Join("; ", result.Errors.Select(e => e.Description));
}
