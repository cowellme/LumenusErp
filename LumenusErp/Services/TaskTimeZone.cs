namespace LumenusErp.Services;

/// <summary>
/// Часовой пояс пользователей трекера (Tasks:TimeZone, по умолчанию Europe/Moscow): в нём модель понимает «до пятницы»
/// и в нём трактуются даты из ответа модели. Нет базы tzdata — запасной фиксированный UTC+3.
/// </summary>
public class TaskTimeZone(IConfiguration config)
{
    public const string DefaultId = "Europe/Moscow";
    private static readonly string[] Weekdays = ["воскресенье", "понедельник", "вторник", "среда", "четверг", "пятница", "суббота"];

    public TimeZoneInfo Info { get; } = Resolve(config["Tasks:TimeZone"]);

    public static TimeZoneInfo Resolve(string? id)
    {
        id = string.IsNullOrWhiteSpace(id) ? DefaultId : id.Trim();
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return TimeZoneInfo.CreateCustomTimeZone(id, TimeSpan.FromHours(3), id, id);
        }
    }

    /// <summary>
    /// Первая строка сообщения для модели: «Сейчас: 2026-10-05, понедельник, 13:15, часовой пояс Europe/Moscow (UTC+03:00)»
    /// (тот же формат, что у myasi).
    /// </summary>
    public static string DateContext(DateTime utcNow, TimeZoneInfo tz)
    {
        var utc = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);
        var local = TimeZoneInfo.ConvertTimeFromUtc(utc, tz);
        var offset = tz.GetUtcOffset(utc);
        var sign = offset < TimeSpan.Zero ? "-" : "+";
        var o = offset.Duration();
        return $"Сейчас: {local:yyyy-MM-dd}, {Weekdays[(int)local.DayOfWeek]}, {local:HH:mm}, " +
               $"часовой пояс {tz.Id} (UTC{sign}{o.Hours:00}:{o.Minutes:00})";
    }
}
