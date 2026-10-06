using LumenusErp.Data;

namespace LumenusErp.Services;

/// <summary>Правила сроков задачи (начало и дедлайн, UTC) без БД: нормализация, порядок, дописывание в дубль, сортировка.</summary>
public static class TaskDates
{
    public const string OrderError = "Начало не может быть позже дедлайна.";

    /// <summary>Любое смещение → UTC; Kind=Utc, чтобы значение писалось и отдавалось одинаково.</summary>
    public static DateTime? ToUtc(DateTimeOffset? v) =>
        v is null ? null : DateTime.SpecifyKind(v.Value.UtcDateTime, DateTimeKind.Utc);

    /// <summary>UTC-время из БД (Kind=Unspecified) → DateTimeOffset с нулевым смещением.</summary>
    public static DateTimeOffset? ToOffset(DateTime? utc) =>
        utc is null ? null : new DateTimeOffset(DateTime.SpecifyKind(utc.Value, DateTimeKind.Utc));

    /// <summary>Текст ошибки (ключ ответа — startAt) или null, если порядок верный. Равные сроки допустимы, один срок тоже.</summary>
    public static string? Validate(DateTime? startUtc, DateTime? dueUtc) =>
        startUtc is { } s && dueUtc is { } d && s > d ? OrderError : null;

    /// <summary>В открытый дубль по названию сроки дописываются, только если у него нет ни одного, а в запросе есть хоть один.</summary>
    public static bool ShouldFillDuplicate(DateTime? dupStart, DateTime? dupDue, DateTime? reqStart, DateTime? reqDue) =>
        dupStart is null && dupDue is null && (reqStart is not null || reqDue is not null);

    /// <summary>
    /// Порядок открытых задач: сначала с дедлайном по возрастанию, затем без дедлайна; внутри равных — новые выше.
    /// </summary>
    public static IEnumerable<TaskItem> SortOpen(IEnumerable<TaskItem> items) =>
        items.OrderBy(t => t.DueAt is null).ThenBy(t => t.DueAt).ThenByDescending(t => t.CreatedAt).ThenBy(t => t.Id);
}
