using System.Text;
using System.Text.Json;

namespace LumenusErp.Services
{
    public class OpenRouterClient
    {
        private readonly HttpClient _httpClient;
        private const string ApiUrl = "https://openrouter.ai/api/v1/chat/completions";
        
        public OpenRouterClient(string apiKey)
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
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
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
        
            try
            {
                var response = await _httpClient.PostAsync(ApiUrl, content);
                response.EnsureSuccessStatusCode();

                var responseBody = await response.Content.ReadAsStringAsync();
                return responseBody;
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                throw;
            }
        }
    }
}
