namespace LumenusErp.Data;

/// <summary>Загруженная запись созвона (вкладка «Созвоны»): обработка идёт в фоне, удаление физическое.</summary>
public class CallRecording
{
    public const string StatusQueued = "queued";
    public const string StatusProcessing = "processing";
    public const string StatusDone = "done";
    public const string StatusFailed = "failed";

    public Guid Id { get; set; }

    /// <summary>Владелец записи (AspNetUsers.Id): каждый видит только свои.</summary>
    public string OwnerId { get; set; } = "";
    public ApplicationUser? Owner { get; set; }

    /// <summary>Исходное имя файла (≤255).</summary>
    public string FileName { get; set; } = "";
    public long SizeBytes { get; set; }

    /// <summary>"queued", "processing", "done" или "failed".</summary>
    public string Status { get; set; } = StatusQueued;

    /// <summary>Текущий шаг для UI: «Извлечение аудио», «Распознавание 2/5», «Выделение задач».</summary>
    public string Stage { get; set; } = "";

    /// <summary>Причина сбоя (failed) или предупреждение при done (например, ИИ не настроен).</summary>
    public string? Error { get; set; }

    public string? Transcript { get; set; }
    public double? DurationSeconds { get; set; }

    /// <summary>Каким промптом выделены задачи: "user" (личный) или "default" (общий); null — задачи ещё не выделялись.</summary>
    public string? PromptSource { get; set; }

    /// <summary>Время последнего изменения применённого промпта (UTC); null — текст из кода или не выделялось.</summary>
    public DateTime? PromptUpdatedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public List<CallTaskSuggestion> Suggestions { get; set; } = new();
}

/// <summary>Задача, найденная моделью в транскрипте; после добавления в трекер хранит ссылку на неё.</summary>
public class CallTaskSuggestion
{
    public Guid Id { get; set; }
    public Guid CallRecordingId { get; set; }
    public CallRecording? CallRecording { get; set; }
    public int Order { get; set; }
    public string Title { get; set; } = "";

    /// <summary>Цитата из транскрипта (≤2000).</summary>
    public string SourceText { get; set; } = "";

    /// <summary>Задача, созданная из этой подсказки; null — ещё не добавлена. При удалении задачи ссылка обнуляется.</summary>
    public Guid? TaskItemId { get; set; }
    public TaskItem? TaskItem { get; set; }
}
