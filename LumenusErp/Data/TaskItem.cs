namespace LumenusErp.Data;

/// <summary>Задача пользователя: от внешнего сервиса (myasi, api/tasks) или заведённая вручную в трекере. Удаление мягкое (DeletedAt).</summary>
public class TaskItem
{
    public const string StatusOpen = "open";
    public const string StatusDone = "done";

    public Guid Id { get; set; }

    /// <summary>Владелец задачи (AspNetUsers.Id): каждый видит только свои.</summary>
    public string OwnerId { get; set; } = "";
    public ApplicationUser? Owner { get; set; }
    public string Title { get; set; } = "";

    /// <summary>Title.Trim().ToLowerInvariant(): ключ поиска открытого дубля.</summary>
    public string TitleNormalized { get; set; } = "";

    /// <summary>"open" или "done".</summary>
    public string Status { get; set; } = StatusOpen;

    public string SourceText { get; set; } = "";
    public string Source { get; set; } = "";
    public string? ExternalId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
}
