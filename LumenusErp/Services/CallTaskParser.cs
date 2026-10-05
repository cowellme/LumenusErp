using System.Text.Json;

namespace LumenusErp.Services;

/// <summary>Найденная в транскрипте задача: название и цитата.</summary>
public record ParsedCallTask(string Title, string Quote);

/// <summary>
/// Разбор ответа модели по промпту "call-tasks": JSON-массив [{"title","quote"}].
/// Устойчив к ```json-ограждениям и тексту вокруг массива.
/// </summary>
public static class CallTaskParser
{
    public const int MaxTitle = 300, MaxQuote = 2000;

    /// <summary>Список задач (возможно пустой) или null, если массив разобрать не удалось.</summary>
    public static List<ParsedCallTask>? Parse(string? raw)
    {
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
                result.Add(new(Cut(title, MaxTitle), Cut(quote, MaxQuote)));
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

    private static string Cut(string s, int max) => s.Length <= max ? s : s[..max];
}
