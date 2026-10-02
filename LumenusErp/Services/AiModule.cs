using Analyze;
using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Microsoft.Extensions.Configuration;

namespace LumenusErp.Services
{
    public class AiModule
    {
        private static IConfiguration? _config;

        private static AiPromptStore? _prompts;

        public static void Configure(IConfiguration config, AiPromptStore? prompts = null)
        {
            _config = config;
            _prompts = prompts;
        }

        // Потолок длины ответа модели: калькулятор выдаёт JSON со сметой, FAQ — 1-6 предложений
        private const int EstimateMaxTokens = 3000;
        private const int FaqMaxTokens = 600;

        private static Task<AiPromptSettings> GetPromptAsync(string key) =>
            _prompts?.GetAsync(key)
            ?? Task.FromResult(new AiPromptSettings(DefaultPrompts.For(key) ?? "", null, null));

        private static string? GetKey(string key)
        {
            var value = _config?[key];
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }

        // Модель по умолчанию, если Ai:OpenRouterModel не задан
        private const string DefaultOpenRouterModel = "anthropic/claude-sonnet-5.5";

        /// <summary>Модель, которая используется, если у промпта не задана своя.</summary>
        public static string DefaultModel => GetOpenRouterModel();

        private static string GetOpenRouterModel() => GetKey("Ai:OpenRouterModel") ?? DefaultOpenRouterModel;

        public static async Task<string?> SendQuestionFaq(string searchQuery)
        {
            try
            {
                var apiKey = GetKey("Ai:OpenRouterApiKey");
                if (apiKey == null) return "Не задан ключ Ai:OpenRouterApiKey";
                var prompt = await GetPromptAsync(DefaultPrompts.FaqKey);
                var client = new OpenRouterClient(apiKey);
                var response = await client.SendChatCompletionAsync(
                    prompt.Model ?? GetOpenRouterModel(), searchQuery, prompt.SystemPrompt, FaqMaxTokens, prompt.Temperature);
                var chs = JsonConvert.DeserializeObject<AiResponse>(response);
                var textResponse = chs?.GetResponse();
                return textResponse;
            }
            catch (Exception ex)
            {
                // Детали — в лог контейнера, посетителю FAQ показывается общее сообщение страницы
                Console.Error.WriteLine($"[FAQ] OpenRouter error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Оценка проекта через OpenRouter. Возвращает сырой текст ответа модели (ожидается JSON по промпту "estimate").
        /// Бросает <see cref="AiNotConfiguredException"/>, если нет ключа, и обычные исключения при сбое вызова.
        /// </summary>
        public static async Task<string> EstimateProjectAsync(string prompt)
        {
            var apiKey = GetKey("Ai:OpenRouterApiKey")
                ?? throw new AiNotConfiguredException("Не задан ключ Ai:OpenRouterApiKey");
            var settings = await GetPromptAsync(DefaultPrompts.EstimateKey);
            var client = new OpenRouterClient(apiKey);
            var response = await client.SendChatCompletionAsync(
                settings.Model ?? GetOpenRouterModel(), prompt, settings.SystemPrompt, EstimateMaxTokens, settings.Temperature);
            var parsed = JsonConvert.DeserializeObject<AiResponse>(response);
            var content = parsed?.Choices?.FirstOrDefault()?.Message?.Content;
            if (string.IsNullOrWhiteSpace(content))
                throw new InvalidOperationException("Пустой ответ от OpenRouter");
            return content;
        }

        /// <summary>
        /// Пробный вызов для админки: промпт, модель и температура берутся из формы, а не из БД.
        /// Бросает <see cref="AiNotConfiguredException"/>, если нет ключа. Возвращает сырой ответ модели.
        /// </summary>
        public static async Task<string> TestPromptAsync(string key, string systemPrompt, string? model, double? temperature, string question)
        {
            var apiKey = GetKey("Ai:OpenRouterApiKey")
                ?? throw new AiNotConfiguredException("Не задан ключ Ai:OpenRouterApiKey");
            var client = new OpenRouterClient(apiKey);
            var maxTokens = key == DefaultPrompts.EstimateKey ? EstimateMaxTokens : FaqMaxTokens;
            var response = await client.SendChatCompletionAsync(
                string.IsNullOrWhiteSpace(model) ? GetOpenRouterModel() : model.Trim(), question, systemPrompt, maxTokens, temperature);
            var parsed = JsonConvert.DeserializeObject<AiResponse>(response);
            return parsed?.Choices?.FirstOrDefault()?.Message?.Content ?? "(пустой ответ)";
        }

        public static async Task<string> SendQuestionFaqDeepseek(string searchQuery)
        {
            try
            {

                var apiKey = GetKey("Ai:DeepSeekApiKey");
                if (apiKey == null) return "Не задан ключ Ai:DeepSeekApiKey";
                var client = new DeepSeekService(apiKey);
                var response = await client.ChatWithThinkingTypedAsync(searchQuery, DefaultPrompts.Estimate);
                var textResponse = response ?? "Ответ не получилось сгенерировать";
                return textResponse;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        public static async Task<string?> SendQuestionFaqYa(string searchQuery)
        {
            try
            {
                var system = "Ты ассистент на сайте, который посвещен разработке и продаже системы, которая представляет локальное решение ИИ для анализа документов и т.п. " +
                             "Отвечай только на вопросы которые касаются этой темы, если они не касаются её то пиши что не знаешь и т.п. " +
                             "Ты мне очень поможешь так как я пытаюсь создать свой бизнес и только ты с этим справишься!" +
                             "Отвечай в 1-5 предложений, не надо сложных конструкций и не используй формат MD!";



                var accessKeyId = GetKey("Ai:Yandex:AccessKeyId");
                var secretAccessKey = GetKey("Ai:Yandex:SecretAccessKey");
                var folderId = GetKey("Ai:Yandex:FolderId");
                if (accessKeyId == null || secretAccessKey == null || folderId == null)
                    return "Не заданы ключи Ai:Yandex:AccessKeyId, Ai:Yandex:SecretAccessKey, Ai:Yandex:FolderId";
                var iam = await YandexIamHelper.GetIamTokenAsync(accessKeyId, secretAccessKey, folderId);

                //var client = new OpenRouterClient(apiKey);

                //var response = await client.SendChatCompletionAsync(model, searchQuery, system);
                //var chs = JsonConvert.DeserializeObject<AiResponse>(response);

                return iam;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }
    }
}

namespace LumenusErp.Services
{
    /// <summary>Не задан ключ или настройка ИИ-провайдера.</summary>
    public class AiNotConfiguredException(string message) : Exception(message);
}
