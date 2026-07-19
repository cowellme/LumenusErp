
namespace Analyze
{
    using System;
    using System.Collections.Generic;

    using System.Globalization;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    public partial class AiResponse
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("object")]
        public string Object { get; set; }

        [JsonProperty("created")]
        public long Created { get; set; }

        [JsonProperty("model")]
        public string Model { get; set; }

        [JsonProperty("provider")]
        public string Provider { get; set; }

        [JsonProperty("system_fingerprint")]
        public object SystemFingerprint { get; set; }

        [JsonProperty("choices")]
        public Choice[] Choices { get; set; }

        [JsonProperty("usage")]
        public Usage Usage { get; set; }

        public string GetResponse()
        {
            return Choices[0].Message.Content.Replace("5 публикаций", "13 публикаций");
        }
    }

    public partial class Choice
    {
        [JsonProperty("index")]
        public long Index { get; set; }

        [JsonProperty("logprobs")]
        public object Logprobs { get; set; }

        [JsonProperty("finish_reason")]
        public string FinishReason { get; set; }

        [JsonProperty("native_finish_reason")]
        public string NativeFinishReason { get; set; }

        [JsonProperty("message")]
        public Message Message { get; set; }
    }

    public partial class Message
    {
        [JsonProperty("role")]
        public string Role { get; set; }

        [JsonProperty("content")]
        public string Content { get; set; }

        [JsonProperty("refusal")]
        public object Refusal { get; set; }

        [JsonProperty("reasoning")]
        public string Reasoning { get; set; }

        [JsonProperty("reasoning_details")]
        public ReasoningDetail[] ReasoningDetails { get; set; }
    }

    public partial class ReasoningDetail
    {
        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("text")]
        public string Text { get; set; }

        [JsonProperty("format")]
        public string Format { get; set; }

        [JsonProperty("index")]
        public long Index { get; set; }
    }

    public partial class Usage
    {
        [JsonProperty("prompt_tokens")]
        public long PromptTokens { get; set; }

        [JsonProperty("completion_tokens")]
        public long CompletionTokens { get; set; }

        [JsonProperty("total_tokens")]
        public long TotalTokens { get; set; }

        [JsonProperty("cost")]
        public long Cost { get; set; }

        [JsonProperty("is_byok")]
        public bool IsByok { get; set; }

        [JsonProperty("prompt_tokens_details")]
        public PromptTokensDetails PromptTokensDetails { get; set; }

        [JsonProperty("cost_details")]
        public CostDetails CostDetails { get; set; }

        [JsonProperty("completion_tokens_details")]
        public CompletionTokensDetails CompletionTokensDetails { get; set; }
    }

    public partial class CompletionTokensDetails
    {
        [JsonProperty("reasoning_tokens")]
        public long ReasoningTokens { get; set; }

        [JsonProperty("image_tokens")]
        public long ImageTokens { get; set; }

        [JsonProperty("audio_tokens")]
        public long AudioTokens { get; set; }
    }

    public partial class CostDetails
    {
        [JsonProperty("upstream_inference_cost")]
        public long UpstreamInferenceCost { get; set; }

        [JsonProperty("upstream_inference_prompt_cost")]
        public long UpstreamInferencePromptCost { get; set; }

        [JsonProperty("upstream_inference_completions_cost")]
        public long UpstreamInferenceCompletionsCost { get; set; }
    }

    public partial class PromptTokensDetails
    {
        [JsonProperty("cached_tokens")]
        public long CachedTokens { get; set; }

        [JsonProperty("cache_write_tokens")]
        public long CacheWriteTokens { get; set; }

        [JsonProperty("audio_tokens")]
        public long AudioTokens { get; set; }

        [JsonProperty("video_tokens")]
        public long VideoTokens { get; set; }
    }
}
