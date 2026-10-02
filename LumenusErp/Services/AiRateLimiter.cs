using System.Net;
using Microsoft.Extensions.Options;

namespace LumenusErp.Services;

/// <summary>Функции ИИ, у которых есть лимиты.</summary>
public enum AiKind
{
    Calculator,
    Faq,
}

/// <summary>Лимиты из секции конфигурации AiLimits; значения по умолчанию подходят для прода.</summary>
public sealed class AiLimitsOptions
{
    public int CalculatorPerHour { get; set; } = 5;
    public int CalculatorPerDay { get; set; } = 15;
    public int FaqPerHour { get; set; } = 20;
    public int FaqPerDay { get; set; } = 60;
    public int CalculatorGlobalPerDay { get; set; } = 300;
    public int FaqGlobalPerDay { get; set; } = 1000;
}

/// <summary>Максимальная длина пользовательского ввода, уходящего в модель.</summary>
public static class AiInputLimits
{
    public const int CalculatorMaxChars = 4000;
    public const int FaqMaxChars = 500;
}

public enum AiLimitStatus
{
    Allowed,
    /// <summary>Исчерпан лимит клиента (IP); <see cref="AiLimitResult.RetryAfter"/> — когда освободится слот.</summary>
    ClientLimited,
    /// <summary>Исчерпан общий суточный лимит сервиса.</summary>
    GloballyLimited,
}

public readonly record struct AiLimitResult(AiLimitStatus Status, TimeSpan RetryAfter)
{
    public bool IsAllowed => Status == AiLimitStatus.Allowed;
}

/// <summary>
/// Скользящие окна (час и сутки) на IP клиента и суточный потолок на весь сервис. Хранится в памяти процесса:
/// после рестарта счётчики обнуляются, что для защиты от расхода бюджета достаточно.
/// </summary>
public sealed class AiRateLimiter(IOptions<AiLimitsOptions> options, ILogger<AiRateLimiter> logger)
{
    private static readonly TimeSpan Hour = TimeSpan.FromHours(1);
    private static readonly TimeSpan Day = TimeSpan.FromDays(1);
    private const int SweepThreshold = 5000;

    private readonly AiLimitsOptions _limits = options.Value;
    private readonly object _lock = new();
    private readonly Dictionary<string, Queue<DateTime>> _hits = new();

    /// <summary>
    /// Проверяет и, если можно, сразу занимает слот. Администратор (<paramref name="isAdmin"/>) не ограничивается и не считается.
    /// </summary>
    public AiLimitResult TryAcquire(AiKind kind, string? clientIp, bool isAdmin)
    {
        if (isAdmin)
        {
            return new AiLimitResult(AiLimitStatus.Allowed, TimeSpan.Zero);
        }

        var (perHour, perDay, global) = kind == AiKind.Calculator
            ? (_limits.CalculatorPerHour, _limits.CalculatorPerDay, _limits.CalculatorGlobalPerDay)
            : (_limits.FaqPerHour, _limits.FaqPerDay, _limits.FaqGlobalPerDay);

        var ip = string.IsNullOrWhiteSpace(clientIp) ? "unknown" : clientIp;
        var clientKey = $"{kind}:{ip}";
        var globalKey = $"{kind}:*";
        var now = DateTime.UtcNow;

        AiLimitResult result;
        lock (_lock)
        {
            if (_hits.Count > SweepThreshold)
            {
                Sweep(now);
            }

            var clientQueue = GetPruned(clientKey, now);
            var globalQueue = GetPruned(globalKey, now);

            var wait = TimeSpan.Zero;
            wait = Max(wait, WaitFor(clientQueue, perHour, Hour, now));
            wait = Max(wait, WaitFor(clientQueue, perDay, Day, now));

            if (wait > TimeSpan.Zero)
            {
                result = new AiLimitResult(AiLimitStatus.ClientLimited, wait);
            }
            else if (WaitFor(globalQueue, global, Day, now) > TimeSpan.Zero)
            {
                result = new AiLimitResult(AiLimitStatus.GloballyLimited, WaitFor(globalQueue, global, Day, now));
            }
            else
            {
                clientQueue.Enqueue(now);
                globalQueue.Enqueue(now);
                return new AiLimitResult(AiLimitStatus.Allowed, TimeSpan.Zero);
            }
        }

        logger.LogWarning("AI limit {Status} for {Kind}, client {Ip}, retry in {Minutes} min",
            result.Status, kind, MaskIp(ip), (int)Math.Ceiling(result.RetryAfter.TotalMinutes));
        return result;
    }

    // Сколько ждать, пока в окне освободится слот; Zero — лимит не исчерпан (limit <= 0 запрещает всё).
    private static TimeSpan WaitFor(Queue<DateTime> queue, int limit, TimeSpan window, DateTime now)
    {
        if (limit <= 0) return window;
        var inWindow = queue.Where(t => now - t < window).ToArray();
        if (inWindow.Length < limit) return TimeSpan.Zero;
        // Слот освободится, когда выйдет из окна (длина - limit + 1)-я по порядку метка
        var blocking = inWindow[inWindow.Length - limit];
        var wait = blocking + window - now;
        return wait > TimeSpan.Zero ? wait : TimeSpan.Zero;
    }

    private static TimeSpan Max(TimeSpan a, TimeSpan b) => a > b ? a : b;

    private Queue<DateTime> GetPruned(string key, DateTime now)
    {
        if (!_hits.TryGetValue(key, out var queue))
        {
            queue = new Queue<DateTime>();
            _hits[key] = queue;
        }
        while (queue.Count > 0 && now - queue.Peek() >= Day)
        {
            queue.Dequeue();
        }
        return queue;
    }

    private void Sweep(DateTime now)
    {
        foreach (var key in _hits.Keys.ToList())
        {
            var queue = _hits[key];
            while (queue.Count > 0 && now - queue.Peek() >= Day)
            {
                queue.Dequeue();
            }
            if (queue.Count == 0)
            {
                _hits.Remove(key);
            }
        }
    }

    /// <summary>1.2.3.4 -> 1.2.3.x; IPv6 -> первые два блока. В лог не попадает полный адрес.</summary>
    internal static string MaskIp(string ip)
    {
        if (!IPAddress.TryParse(ip, out var addr)) return "unknown";
        if (addr.IsIPv4MappedToIPv6) addr = addr.MapToIPv4();
        var s = addr.ToString();
        if (addr.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            var i = s.LastIndexOf('.');
            return s[..i] + ".x";
        }
        var parts = s.Split(':');
        return string.Join(':', parts.Take(2)) + "::x";
    }
}
