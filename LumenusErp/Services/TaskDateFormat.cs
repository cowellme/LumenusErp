using System.Globalization;

namespace LumenusErp.Services;

/// <summary>Подпись сроков задачи: Start/Due — готовые куски («с 12 окт», «до 9 окт, 10:00»), Overdue — дедлайн открытой задачи прошёл.</summary>
public record TaskDateLabel(string? Start, string? Due, bool Overdue);

/// <summary>Показ сроков в часовом поясе пользователя и перевод местного времени из полей ввода в UTC (смещение берётся из браузера).</summary>
public static class TaskDateFormat
{
    private static readonly string[] Months = ["янв", "фев", "мар", "апр", "мая", "июн", "июл", "авг", "сен", "окт", "ноя", "дек"];

    // Только день: начало = 00:00, дедлайн = 23:59 местного времени (то же соглашение, что у myasi).
    public static readonly TimeOnly DefaultStartTime = new(0, 0);
    public static readonly TimeOnly DefaultDueTime = new(23, 59);

    public static DateTime ToLocal(DateTime utc, int offsetMinutes) => utc.AddMinutes(offsetMinutes);

    public static TaskDateLabel Format(DateTime? startUtc, DateTime? dueUtc, bool open, DateTime nowUtc, int offsetMinutes)
    {
        var nowYear = ToLocal(nowUtc, offsetMinutes).Year;
        var start = startUtc is { } s ? "с " + Part(ToLocal(s, offsetMinutes), DefaultStartTime, nowYear) : null;
        var due = dueUtc is { } d ? "до " + Part(ToLocal(d, offsetMinutes), DefaultDueTime, nowYear) : null;
        var overdue = open && dueUtc is { } du && du < nowUtc;
        return new(start, due, overdue);
    }

    private static string Part(DateTime local, TimeOnly hiddenTime, int nowYear)
    {
        var text = $"{local.Day} {Months[local.Month - 1]}";
        if (local.Year != nowYear) text += " " + local.Year.ToString(CultureInfo.InvariantCulture);
        if (TimeOnly.FromDateTime(local) != hiddenTime) text += ", " + local.ToString("HH:mm", CultureInfo.InvariantCulture);
        return text;
    }

    /// <summary>Значения для полей ввода: дата "yyyy-MM-dd" и время "HH:mm" (пустое, если время по умолчанию).</summary>
    public static (string Date, string Time) ToInputs(DateTime? utc, bool isDue, int offsetMinutes)
    {
        if (utc is null) return ("", "");
        var local = ToLocal(utc.Value, offsetMinutes);
        var hidden = isDue ? DefaultDueTime : DefaultStartTime;
        return (local.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            TimeOnly.FromDateTime(local) == hidden ? "" : local.ToString("HH:mm", CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Поля ввода → UTC. Пустая дата = срока нет (true, null). Пустое время = 00:00 для начала и 23:59 для дедлайна.
    /// Нечитаемая дата или время — false.
    /// </summary>
    public static bool TryFromInputs(string? date, string? time, bool isDue, int offsetMinutes, out DateTime? utc)
    {
        utc = null;
        if (string.IsNullOrWhiteSpace(date)) return true;
        if (!DateOnly.TryParseExact(date.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)) return false;
        var t = isDue ? DefaultDueTime : DefaultStartTime;
        if (!string.IsNullOrWhiteSpace(time) && !TimeOnly.TryParseExact(time.Trim(), ["HH:mm", "HH:mm:ss"], CultureInfo.InvariantCulture, DateTimeStyles.None, out t)) return false;
        utc = DateTime.SpecifyKind(d.ToDateTime(t).AddMinutes(-offsetMinutes), DateTimeKind.Utc);
        return true;
    }
}
