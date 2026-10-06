namespace LumenusErp.Data;

/// <summary>Личный системный промпт пользователя: перекрывает общий из <see cref="AiPrompt"/> с тем же ключом.</summary>
public class UserAiPrompt
{
    public int Id { get; set; }

    /// <summary>Владелец (AspNetUsers.Id); пара (OwnerId, Key) уникальна.</summary>
    public string OwnerId { get; set; } = "";
    public ApplicationUser? Owner { get; set; }

    /// <summary>"myasi-tasks" или "call-tasks".</summary>
    public string Key { get; set; } = "";
    public string SystemPrompt { get; set; } = "";

    /// <summary>Модель OpenRouter; null — как у общего промпта / Ai:OpenRouterModel.</summary>
    public string? Model { get; set; }

    /// <summary>Температура 0–2; null — значение провайдера по умолчанию.</summary>
    public double? Temperature { get; set; }

    public DateTime UpdatedAt { get; set; }
}
