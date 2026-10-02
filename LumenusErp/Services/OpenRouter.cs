using System.Text;
using System.Text.Json;

namespace LumenusErp.Services
{
    public class OpenRouterClient
    {
        // Один HttpClient на процесс, чтобы не исчерпывать сокеты
        private static readonly HttpClient SharedHttpClient = new() { Timeout = TimeSpan.FromSeconds(120) };
        private const string ApiUrl = "https://openrouter.ai/api/v1/chat/completions";
        private const int MaxErrorBodyLength = 500;

        private readonly string _apiKey;

        public OpenRouterClient(string apiKey)
        {
            _apiKey = apiKey;
        }

        public async Task<string> SendChatCompletionAsync(string model, string message, string system)
        {
            var requestData = new
            {
                model,
                messages = new[]
                {
                    new
                    {
                        role = "system",
                        content = system
                    },
                    new
                    {
                        role = "user",
                        content = message
                    }
                }
            };

            var jsonContent = JsonSerializer.Serialize(requestData);

            using var request = new HttpRequestMessage(HttpMethod.Post, ApiUrl)
            {
                Content = new StringContent(jsonContent, Encoding.UTF8, "application/json")
            };
            request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {_apiKey}");
            request.Headers.TryAddWithoutValidation("HTTP-Referer", "https://lumenustech.ru");
            request.Headers.TryAddWithoutValidation("X-Title", "LumenusTech");

            using var response = await SharedHttpClient.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                var snippet = responseBody.Length > MaxErrorBodyLength
                    ? responseBody[..MaxErrorBodyLength] + "..."
                    : responseBody;
                throw new HttpRequestException(
                    $"OpenRouter вернул {(int)response.StatusCode} {response.StatusCode}: {snippet}",
                    null,
                    response.StatusCode);
            }

            return responseBody;
        }
    }
}
