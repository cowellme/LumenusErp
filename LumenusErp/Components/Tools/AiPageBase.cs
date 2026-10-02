using LumenusErp.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace LumenusErp.Components.Tools;

/// <summary>
/// Базовый класс страниц с платными вызовами ИИ (калькулятор, FAQ): определяет IP клиента и проверяет лимиты.
/// </summary>
/// <remarks>
/// В интерактивной фазе Blazor Server HttpContext недоступен, поэтому IP берётся из HttpContext при пререндере
/// (там уже отработал UseForwardedHeaders, то есть это адрес за Caddy, а не docker-шлюз) и переносится в интерактивную
/// фазу через PersistentComponentState; состояние шифруется Data Protection, клиент его подделать не может.
/// Если пререндера не было (прямое SignalR-подключение), IP неизвестен и клиент попадает в общую корзину "unknown".
/// </remarks>
public abstract class AiPageBase : ComponentBase, IDisposable
{
    private const string StateKey = "client-ip";

    private PersistingComponentStateSubscription _persisting;
    private string? _clientIp;

    [CascadingParameter] public HttpContext? HttpContext { get; set; }

    [Inject] private PersistentComponentState ApplicationState { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthProvider { get; set; } = default!;
    [Inject] private AiRateLimiter RateLimiter { get; set; } = default!;

    protected override void OnInitialized()
    {
        if (ApplicationState.TryTakeFromJson<string>(StateKey, out var persisted))
        {
            _clientIp = persisted;
            return;
        }

        _clientIp = HttpContext?.Connection.RemoteIpAddress?.ToString();
        _persisting = ApplicationState.RegisterOnPersisting(() =>
        {
            ApplicationState.PersistAsJson(StateKey, _clientIp);
            return Task.CompletedTask;
        });
    }

    /// <summary>Занимает слот лимита для текущего клиента; администраторы не ограничиваются.</summary>
    protected async Task<AiLimitResult> TryAcquireAsync(AiKind kind)
    {
        var state = await AuthProvider.GetAuthenticationStateAsync();
        return RateLimiter.TryAcquire(kind, _clientIp, state.User.IsInRole("Admin"));
    }

    /// <summary>Текст для посетителя при отказе по лимиту.</summary>
    protected static string LimitMessage(AiLimitResult limit)
    {
        if (limit.Status == AiLimitStatus.GloballyLimited)
        {
            return "Сервис временно перегружен, напишите нам в Telegram.";
        }
        var minutes = Math.Max(1, (int)Math.Ceiling(limit.RetryAfter.TotalMinutes));
        if (minutes >= 120)
        {
            var hours = (int)Math.Ceiling(minutes / 60.0);
            return $"Слишком много запросов, попробуйте через {hours} {HoursWord(hours)}.";
        }
        return $"Слишком много запросов, попробуйте через {minutes} {MinutesWord(minutes)}.";
    }

    private static string HoursWord(int n) => (n % 100) switch
    {
        >= 11 and <= 14 => "часов",
        _ => (n % 10) switch { 1 => "час", 2 or 3 or 4 => "часа", _ => "часов" },
    };

    private static string MinutesWord(int n) => (n % 100) switch
    {
        >= 11 and <= 14 => "минут",
        _ => (n % 10) switch { 1 => "минуту", 2 or 3 or 4 => "минуты", _ => "минут" },
    };

    public void Dispose() => _persisting.Dispose();
}
