using LumenusErp.Data;
using Microsoft.EntityFrameworkCore;

namespace LumenusErp.Services;

/// <summary>Выбранный для пользователя промпт. Source: "user" — личный, "default" — общий (или текст из кода, тогда UpdatedAt = MinValue).</summary>
public sealed record ResolvedPrompt(string Key, string Text, string? Model, double? Temperature, DateTime UpdatedAt, string Source);

/// <summary>Результат сохранения личного промпта: либо Error (по-русски), либо сохранённая строка.</summary>
public sealed record SavePromptResult(UserAiPrompt? Item, string? Error);

/// <summary>Чистая логика личных промптов (без БД).</summary>
public static class UserPromptRules
{
    public const string SourceUser = "user";
    public const string SourceDefault = "default";
    public const int MaxText = 50_000;
    public const int MaxModel = 200;

    public static readonly string[] PersonalKeys = [DefaultPrompts.MyasiTasksKey, DefaultPrompts.CallTasksKey];

    public static bool IsPersonalKey(string? key) => key is not null && PersonalKeys.Contains(key);

    /// <summary>Свой (если есть и не пустой) → общий из БД (если не пустой) → текст из кода.</summary>
    public static ResolvedPrompt Resolve(UserAiPrompt? own, AiPrompt? common, string key)
    {
        if (own is not null && !string.IsNullOrWhiteSpace(own.SystemPrompt))
        {
            return new ResolvedPrompt(key, own.SystemPrompt, CleanModel(own.Model), own.Temperature, own.UpdatedAt, SourceUser);
        }
        if (common is not null && !string.IsNullOrWhiteSpace(common.SystemPrompt))
        {
            return new ResolvedPrompt(key, common.SystemPrompt, CleanModel(common.Model), common.Temperature, common.UpdatedAt, SourceDefault);
        }
        return new ResolvedPrompt(key, DefaultPrompts.For(key) ?? "", null, null, DateTime.MinValue, SourceDefault);
    }

    /// <summary>Слабый ETag в кавычках: меняется со сменой ключа, источника или времени изменения.</summary>
    public static string ETag(ResolvedPrompt p) => $"\"{p.Key}-{p.Source}-{p.UpdatedAt.Ticks}\"";

    /// <summary>Ошибка валидации личного промпта или null.</summary>
    public static string? Validate(string? text, string? model, double? temperature)
    {
        if (string.IsNullOrWhiteSpace(text)) return "Текст промпта не может быть пустым.";
        if (text.Length > MaxText) return $"Текст промпта не длиннее {MaxText} символов.";
        if (temperature is < 0 or > 2 || temperature is double.NaN) return "Температура: число от 0 до 2.";
        if (model is not null && model.Trim().Length > MaxModel) return $"Модель: не длиннее {MaxModel} символов.";
        return null;
    }

    /// <summary>Решение для GET /api/prompts: true — If-None-Match совпал, отвечать 304.</summary>
    public static bool NotModified(string? ifNoneMatch, string etag) =>
        !string.IsNullOrWhiteSpace(ifNoneMatch)
        && ifNoneMatch.Split(',').Select(v => v.Trim()).Any(v => v == "*" || v == etag || v == "W/" + etag);

    private static string? CleanModel(string? m) => string.IsNullOrWhiteSpace(m) ? null : m.Trim();
}

/// <summary>Личные промпты пользователей: выбор действующего, чтение, сохранение, сброс. Всегда фильтрует по владельцу.</summary>
public sealed class UserPromptService(IDbContextFactory<ApplicationDbContext> dbFactory)
{
    /// <summary>Действующий промпт пользователя для ключа.</summary>
    public async Task<ResolvedPrompt> GetForUserAsync(string ownerId, string key, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var own = await db.UserAiPrompts.AsNoTracking().FirstOrDefaultAsync(p => p.OwnerId == ownerId && p.Key == key, ct);
        var common = await db.AiPrompts.AsNoTracking().FirstOrDefaultAsync(p => p.Key == key, ct);
        return UserPromptRules.Resolve(own, common, key);
    }

    /// <summary>Личный промпт и общий разом (для страницы «Мои промпты»): общий уже с запасным текстом из кода.</summary>
    public async Task<(UserAiPrompt? Own, ResolvedPrompt Common)> GetBothAsync(string ownerId, string key, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var own = await db.UserAiPrompts.AsNoTracking().FirstOrDefaultAsync(p => p.OwnerId == ownerId && p.Key == key, ct);
        var common = await db.AiPrompts.AsNoTracking().FirstOrDefaultAsync(p => p.Key == key, ct);
        return (own, UserPromptRules.Resolve(null, common, key));
    }

    public async Task<UserAiPrompt?> GetOwnAsync(string ownerId, string key, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.UserAiPrompts.AsNoTracking().FirstOrDefaultAsync(p => p.OwnerId == ownerId && p.Key == key, ct);
    }

    public async Task<SavePromptResult> SaveOwnAsync(string ownerId, string key, string? text, string? model, double? temperature, CancellationToken ct = default)
    {
        if (!UserPromptRules.IsPersonalKey(key)) return new SavePromptResult(null, "Для этого промпта личная настройка недоступна.");
        var error = UserPromptRules.Validate(text, model, temperature);
        if (error is not null) return new SavePromptResult(null, error);

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var row = await db.UserAiPrompts.FirstOrDefaultAsync(p => p.OwnerId == ownerId && p.Key == key, ct);
        if (row is null)
        {
            row = new UserAiPrompt { OwnerId = ownerId, Key = key };
            db.UserAiPrompts.Add(row);
        }
        row.SystemPrompt = text!;
        row.Model = string.IsNullOrWhiteSpace(model) ? null : model.Trim();
        row.Temperature = temperature;
        row.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return new SavePromptResult(row, null);
    }

    /// <summary>Удаляет личный промпт (возврат к общему). false — своего не было.</summary>
    public async Task<bool> ResetAsync(string ownerId, string key, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.UserAiPrompts.Where(p => p.OwnerId == ownerId && p.Key == key).ExecuteDeleteAsync(ct) > 0;
    }
}
