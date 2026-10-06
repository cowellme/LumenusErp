using System.Globalization;
using System.Text.Json;

namespace LumenusErp.Services;

/// <summary>Найденная в транскрипте задача: название, цитата и необязательные сроки (UTC).</summary>
public record ParsedCallTask(string Title, string Quote, DateTime? StartAt = null, DateTime? DueAt = null);

/// <summary>
/// Разбор ответа модели по промпту "call-tasks": JSON-массив [{"title","quote","start","deadline"}];
/// start/deadline — "YYYY-MM-DD" или "YYYY-MM-DDTHH:MM" местного времени <c>tz</c> либо null. Только день: начало 00:00, дедлайн 23:59.
/// Устойчив к ```json-ограждениям и тексту вокруг массива.
/// </summary>
public static class CallTaskParser
{
    public const int MaxTitle = 300, MaxQuote = 2000;

    /// <summary>Список задач (возможно пустой) или null, если массив разобрать не удалось.</summary>
    public static List<ParsedCallTask>? Parse(string? raw, TimeZoneInfo? tz = null)
    {
        tz ??= TaskTimeZone.Resolve(null);
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var start = raw.IndexOf('[');
        var end = raw.LastIndexOf(']');
        if (start < 0 || end <= start) return null;

        try
        {
            using var doc = JsonDocument.Parse(raw[start..(end + 1)]);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return null;
            var result = new List<ParsedCallTask>();
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object) continue;
                var title = Str(item, "title").Trim();
                if (title.Length == 0) continue;
                var quote = Str(item, "quote").Trim();
                var startAt = ParseLocal(Str(item, "start"), TaskDateFormat.DefaultStartTime, tz);
                var dueAt = ParseLocal(Str(item, "deadline"), TaskDateFormat.DefaultDueTime, tz);
                if (startAt > dueAt) startAt = null;
                result.Add(new(Cut(title, MaxTitle), Cut(quote, MaxQuote), startAt, dueAt));
            }
            return result;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string Str(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

    private static readonly string[] DateFormats = ["yyyy-MM-dd", "yyyy-MM-ddTHH:mm", "yyyy-MM-ddTHH:mm:ss"];

    /// <summary>Местная дата/дата-время → UTC; невалидное или пустое — null. Без времени подставляется <paramref name="defaultTime"/>.</summary>
    private static DateTime? ParseLocal(string value, TimeOnly defaultTime, TimeZoneInfo tz)
    {
        value = value.Trim();
        if (!DateTime.TryParseExact(value, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var local)) return null;
        if (value.Length == 10) local = local.Date + defaultTime.ToTimeSpan();
        try
        {
            return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), tz);
        }
        catch (ArgumentException)
        {
            return null; // несуществующее местное время (переход на летнее)
        }
    }

    private static string Cut(string s, int max) => s.Length <= max ? s : s[..max];
}
