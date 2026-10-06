using System.Security.Claims;
using LumenusErp.Data;

namespace LumenusErp.Services;

/// <summary>
/// Права на контентные страницы и их картинки: Admin — всё, Creator — только свои страницы (<c>CreatedById</c>).
/// Без БД, чтобы покрываться юнит-тестами; страницы и эндпоинты только вызывают эти методы.
/// </summary>
public static class ContentPageAccess
{
    public const string AdminRole = "Admin";
    public const string CreatorRole = "Creator";

    public static bool IsAdmin(ClaimsPrincipal? user) => user?.IsInRole(AdminRole) == true;

    public static bool IsCreator(ClaimsPrincipal? user) => user?.IsInRole(CreatorRole) == true;

    /// <summary>Id пользователя (claim NameIdentifier); null — аноним или claim нет.</summary>
    public static string? UserId(ClaimsPrincipal? user)
    {
        var id = user?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return string.IsNullOrEmpty(id) ? null : id;
    }

    /// <summary>Доступ к конструктору страниц (/admin/pages).</summary>
    public static bool CanUseEditor(ClaimsPrincipal? user) => IsAdmin(user) || IsCreator(user);

    /// <summary>Править, публиковать и удалять страницу: Admin — любую, Creator — только свою.</summary>
    public static bool CanEdit(ClaimsPrincipal? user, string? pageCreatedById)
    {
        if (user?.Identity?.IsAuthenticated != true)
        {
            return false;
        }
        if (IsAdmin(user))
        {
            return true;
        }
        var id = UserId(user);
        return IsCreator(user) && id is not null && pageCreatedById is not null && id == pageCreatedById;
    }

    /// <summary>Черновик видит тот, кто может его править.</summary>
    public static bool CanViewDraft(ClaimsPrincipal? user, string? pageCreatedById) => CanEdit(user, pageCreatedById);

    /// <summary>
    /// Отдача /media/{id}: файл публичной страницы — всем; Admin — всё; вошедшему — файл страницы «для вошедших»;
    /// Creator — файл своей страницы или загруженный им самим (превью до сохранения).
    /// </summary>
    public static bool CanSeeMedia(
        ClaimsPrincipal? user,
        IReadOnlyCollection<PageVisibility> visibilities,
        IReadOnlyCollection<string?> pageOwnerIds,
        string? uploadedById)
    {
        if (visibilities.Contains(PageVisibility.Public))
        {
            return true;
        }
        if (user?.Identity?.IsAuthenticated != true)
        {
            return false;
        }
        if (IsAdmin(user) || visibilities.Contains(PageVisibility.Authenticated))
        {
            return true;
        }
        var id = UserId(user);
        return IsCreator(user) && id is not null && (pageOwnerIds.Contains(id) || uploadedById == id);
    }
}
