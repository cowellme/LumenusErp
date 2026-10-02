using System.Collections.Concurrent;
using LumenusErp.Data;
using Microsoft.EntityFrameworkCore;

namespace LumenusErp.Services;

/// <summary>Настройки одного промпта, достаточные для вызова модели.</summary>
public sealed record AiPromptSettings(string SystemPrompt, string? Model, double? Temperature);

/// <summary>
/// Промпты из БД с коротким кэшем в памяти. AiModule статический, поэтому доступ к БД идёт через singleton-хранилище.
/// Если строки нет или БД недоступна — запасной текст из <see cref="DefaultPrompts"/>.
/// </summary>
public sealed class AiPromptStore(IDbContextFactory<ApplicationDbContext> dbFactory, ILogger<AiPromptStore> logger)
{
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(30);

    private readonly ConcurrentDictionary<string, (DateTime ExpiresAt, AiPromptSettings Settings)> _cache = new();

    public async Task<AiPromptSettings> GetAsync(string key)
    {
        if (_cache.TryGetValue(key, out var hit) && hit.ExpiresAt > DateTime.UtcNow)
        {
            return hit.Settings;
        }

        var fallback = new AiPromptSettings(DefaultPrompts.For(key) ?? "", null, null);
        try
        {
            await using var db = await dbFactory.CreateDbContextAsync();
            var row = await db.AiPrompts.AsNoTracking().FirstOrDefaultAsync(p => p.Key == key);
            var settings = row is null || string.IsNullOrWhiteSpace(row.SystemPrompt)
                ? fallback
                : new AiPromptSettings(row.SystemPrompt, string.IsNullOrWhiteSpace(row.Model) ? null : row.Model.Trim(), row.Temperature);
            _cache[key] = (DateTime.UtcNow + Ttl, settings);
            return settings;
        }
        catch (Exception ex)
        {
            // Сбой БД не должен ронять калькулятор и FAQ; не кэшируем, чтобы не залипнуть на запасном тексте
            logger.LogError(ex, "Не удалось прочитать промпт {Key}, используется текст по умолчанию", key);
            return fallback;
        }
    }

    /// <summary>Сбросить кэш после сохранения промпта в админке.</summary>
    public void Invalidate() => _cache.Clear();
}
