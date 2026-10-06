using System.Globalization;
using System.Text.Json;

namespace LumenusErp.Services;

/// <summary>Модель из каталога OpenRouter. Цены — доллары за 1 млн токенов, null — переменная или неизвестна.</summary>
public sealed record OpenRouterModel(
    string Id, string Name, int? ContextLength, decimal? PromptPricePerM, decimal? CompletionPricePerM,
    IReadOnlyList<string> InputModalities, IReadOnlyList<string> OutputModalities);

/// <summary>Каталог моделей OpenRouter (публичный список, ключ не нужен) с кэшем на час.</summary>
public sealed class OpenRouterCatalog(IHttpClientFactory httpFactory, ILogger<OpenRouterCatalog> logger)
{
    public const string HttpClientName = "openrouter-catalog";
    public const string Url = "https://openrouter.ai/api/v1/models";

    private static readonly TimeSpan Ttl = TimeSpan.FromHours(1);

    private readonly SemaphoreSlim _gate = new(1, 1);
    private List<OpenRouterModel>? _models;
    private DateTime _expiresAt;

    /// <summary>Список моделей и признак ошибки. При сбое отдаётся последний удачный список (если был); ошибка не кэшируется.</summary>
    public async Task<(List<OpenRouterModel> Models, bool Failed)> GetAsync(CancellationToken ct = default)
    {
        if (_models is not null && _expiresAt > DateTime.UtcNow) return (_models, false);

        await _gate.WaitAsync(ct);
        try
        {
            if (_models is not null && _expiresAt > DateTime.UtcNow) return (_models, false);
            try
            {
                var json = await httpFactory.CreateClient(HttpClientName).GetStringAsync(Url, ct);
                var list = Parse(json);
                if (list.Count == 0) throw new InvalidOperationException("Пустой каталог моделей");
                _models = list;
                _expiresAt = DateTime.UtcNow + Ttl;
                return (list, false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Не удалось загрузить каталог моделей OpenRouter");
                return (_models ?? [], true);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Разбор ответа /api/v1/models; мусор даёт пустой список.</summary>
    public static List<OpenRouterModel> Parse(string? json)
    {
        var result = new List<OpenRouterModel>();
        if (string.IsNullOrWhiteSpace(json)) return result;
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object
                || !doc.RootElement.TryGetProperty("data", out var data)
                || data.ValueKind != JsonValueKind.Array) return result;

            foreach (var el in data.EnumerateArray())
            {
                if (el.ValueKind != JsonValueKind.Object) continue;
                var id = Str(el, "id");
                if (string.IsNullOrWhiteSpace(id)) continue;

                int? ctx = el.TryGetProperty("context_length", out var c) && c.ValueKind == JsonValueKind.Number && c.TryGetInt32(out var ci) ? ci : null;
                decimal? prompt = null, completion = null;
                if (el.TryGetProperty("pricing", out var pr) && pr.ValueKind == JsonValueKind.Object)
                {
                    prompt = PerMillion(Str(pr, "prompt"));
                    completion = PerMillion(Str(pr, "completion"));
                }
                IReadOnlyList<string> input = [], output = [];
                if (el.TryGetProperty("architecture", out var ar) && ar.ValueKind == JsonValueKind.Object)
                {
                    input = Strings(ar, "input_modalities");
                    output = Strings(ar, "output_modalities");
                }
                result.Add(new OpenRouterModel(id, Str(el, "name") ?? id, ctx, prompt, completion, input, output));
            }
        }
        catch (JsonException)
        {
            return [];
        }
        return result;
    }

    /// <summary>Модели, подходящие этапу: audio — принимают аудио, text — принимают текст; выдают текст. По имени.</summary>
    public static List<OpenRouterModel> ForStage(IEnumerable<OpenRouterModel> models, string kind)
    {
        var need = kind == AiStages.Audio ? "audio" : "text";
        return models
            .Where(m => m.InputModalities.Contains(need) && m.OutputModalities.Contains("text"))
            .OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(m => m.Id, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>Поиск по id и имени без учёта регистра; пустой запрос — все.</summary>
    public static List<OpenRouterModel> Search(IEnumerable<OpenRouterModel> models, string? query)
    {
        var q = query?.Trim();
        if (string.IsNullOrEmpty(q)) return models.ToList();
        return models
            .Where(m => m.Id.Contains(q, StringComparison.OrdinalIgnoreCase) || m.Name.Contains(q, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private static string? Str(JsonElement el, string name) =>
        el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static List<string> Strings(JsonElement el, string name) =>
        el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Array
            ? v.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()!).ToList()
            : [];

    /// <summary>"$ за токен" → "$ за 1M"; отрицательная (-1), пустая или невалидная цена → null.</summary>
    private static decimal? PerMillion(string? s) =>
        decimal.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) && d >= 0 ? d * 1_000_000m : null;
}
