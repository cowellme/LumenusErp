using System.Net;
using System.Text.Json;

namespace LumenusErp.Services;

/// <summary>Ошибка распознавания с текстом, который можно показать пользователю.</summary>
public class MyasiException(string message) : Exception(message);

/// <summary>Клиент внешнего сервиса распознавания речи myasi (Myasi:BaseUrl, по умолчанию http://myasi:8000).</summary>
public class MyasiClient(HttpClient http)
{
    /// <summary>Отправляет WAV (16 кГц, моно, 16 бит) на распознавание, возвращает текст.</summary>
    public async Task<string> TranscribeAsync(string wavPath, CancellationToken ct)
    {
        try
        {
            await using var fs = new FileStream(wavPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
            using var content = new StreamContent(fs);
            content.Headers.ContentType = new("audio/wav");
            // Служебный эндпоинт: только текст, без задач и без отправки их в Lumenus; токен — заголовок клиента (Myasi:Token)
            using var response = await http.PostAsync("api/transcribe", content, ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.NotFound)
            {
                throw new MyasiException("Сервис распознавания не принял служебный токен: проверьте Myasi:Token (MYASI_TOKEN) и TRANSCRIBE_TOKEN в myasi");
            }
            if (!response.IsSuccessStatusCode)
            {
                throw new MyasiException($"Сервис распознавания вернул {(int)response.StatusCode}: {Detail(body, response.StatusCode)}");
            }
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.TryGetProperty("text", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() ?? "" : "";
        }
        catch (HttpRequestException ex)
        {
            throw new MyasiException($"Сервис распознавания недоступен ({ex.Message})");
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new MyasiException("Сервис распознавания не ответил вовремя");
        }
        catch (JsonException)
        {
            throw new MyasiException("Сервис распознавания вернул некорректный ответ");
        }
    }

    private static string Detail(string body, HttpStatusCode status)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("detail", out var d))
                return d.ValueKind == JsonValueKind.String ? d.GetString() ?? "" : d.ToString();
        }
        catch (JsonException) { }
        return body.Length > 200 ? body[..200] + "…" : (body.Length > 0 ? body : status.ToString());
    }
}
