namespace LumenusErp.Data;

/// <summary>Системный промпт ИИ-функции сайта; тексты правит администратор в /admin/prompts.</summary>
public class AiPrompt
{
    public int Id { get; set; }

    /// <summary>Идентификатор функции: "estimate" (калькулятор), "faq" (FAQ-ассистент); уникален.</summary>
    public string Key { get; set; } = "";
    public string Title { get; set; } = "";
    public string SystemPrompt { get; set; } = "";

    /// <summary>Модель OpenRouter для этого промпта; null — Ai:OpenRouterModel.</summary>
    public string? Model { get; set; }

    /// <summary>Температура 0–2; null — значение провайдера по умолчанию.</summary>
    public double? Temperature { get; set; }

    public DateTime UpdatedAt { get; set; }
}
