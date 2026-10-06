using LumenusErp.Data;

namespace LumenusErp.Services;

/// <summary>Описание этапа конвейера ИИ. HasPrompt — модель хранится в AiPrompts.Model, иначе в AiStageModels.</summary>
public sealed record AiStage(string Key, string Title, string Kind, bool HasPrompt, string DefaultNote);

/// <summary>Все этапы, для которых в /admin/models выбирается модель.</summary>
public static class AiStages
{
    public const string Text = "text";
    public const string Audio = "audio";

    public const string MyasiDedupKey = "myasi-dedup";
    public const string MyasiAsrKey = "myasi-asr";
    public const string DiarizationKey = "diarization";

    private const string PromptDefault = "Ai:OpenRouterModel";

    public static readonly IReadOnlyList<AiStage> All =
    [
        new(DefaultPrompts.EstimateKey, "Калькулятор", Text, true, PromptDefault),
        new(DefaultPrompts.FaqKey, "FAQ-ассистент", Text, true, PromptDefault),
        new(DefaultPrompts.CallTasksKey, "Задачи из созвонов", Text, true, PromptDefault),
        new(DefaultPrompts.MyasiTasksKey, "Задачи с устройства (myasi)", Text, true, PromptDefault),
        new(MyasiDedupKey, "Проверка дублей задач (myasi)", Text, false,
            "как в env myasi: OPENROUTER_DEDUP_MODEL → OPENROUTER_MODEL → deepseek/deepseek-v4.1-flash"),
        new(MyasiAsrKey, "Распознавание речи (myasi)", Audio, false,
            "как в env myasi: OPENROUTER_ASR_MODEL → google/gemini-3.5-flash-lite"),
        new(DiarizationKey, "Разделение голосов (на будущее)", Audio, false, "не используется"),
    ];

    public static AiStage? Find(string? key) => All.FirstOrDefault(s => s.Key == key);
}
