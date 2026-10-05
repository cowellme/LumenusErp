using System.Security.Claims;
using LumenusErp.Data;
using LumenusErp.Services.English;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace LumenusErp.Components.English;

/// <summary>
/// Базовый класс интерактивных страниц кабинета English Studio: текущий пользователь, его роль, профиль и часовой пояс.
/// Наследник загружает данные в <see cref="LoadAsync"/>; до её завершения <see cref="Loaded"/> = false.
/// </summary>
public abstract class EnglishPageBase : ComponentBase
{
    [Inject] protected AuthenticationStateProvider AuthProvider { get; set; } = default!;
    [Inject] protected EnglishProfileService Profiles { get; set; } = default!;
    [Inject] protected NavigationManager Navigation { get; set; } = default!;

    protected string UserId { get; private set; } = "";
    protected bool IsTeacher { get; private set; }
    protected EnglishProfile Me { get; private set; } = new();
    protected TimeZoneInfo Tz { get; set; } = TimeZoneInfo.Utc;
    protected bool Loaded { get; private set; }

    /// <summary>Сообщения об ошибке и успехе последней операции.</summary>
    protected string? Error { get; set; }
    protected string? Notice { get; set; }

    protected override async Task OnInitializedAsync()
    {
        var state = await AuthProvider.GetAuthenticationStateAsync();
        UserId = state.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        IsTeacher = state.User.IsInRole(EnglishRoles.Teacher);
        Me = await Profiles.GetOrCreateAsync(UserId);
        Tz = EnglishRoles.FindTimeZone(Me.TimeZone);
        await LoadAsync();
        Loaded = true;
    }

    /// <summary>Загрузка данных страницы; вызывается и повторно после изменений.</summary>
    protected virtual Task LoadAsync() => Task.CompletedTask;

    protected DateTime Local(DateTime utc) => EnglishRoles.ToLocal(utc, Tz);
    protected DateTime NowLocal => Local(DateTime.UtcNow);

    /// <summary>Показывает результат операции: ошибку или текст успеха.</summary>
    protected bool Report(EnglishResult result, string? success = null)
    {
        Error = result.Error;
        Notice = result.Ok ? success : null;
        return result.Ok;
    }

    protected void ClearMessages()
    {
        Error = null;
        Notice = null;
    }
}
