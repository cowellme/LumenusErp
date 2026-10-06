namespace LumenusErp.Data;

/// <summary>Модель OpenRouter для этапа конвейера без собственного промпта (проверка дублей, распознавание речи, диаризация); правится в /admin/models.</summary>
public class AiStageModel
{
    public int Id { get; set; }

    /// <summary>Ключ этапа из <c>AiStages</c>; уникален.</summary>
    public string Stage { get; set; } = "";

    /// <summary>Модель OpenRouter; null — по умолчанию (для myasi — как в его env).</summary>
    public string? Model { get; set; }

    public DateTime UpdatedAt { get; set; }
}
