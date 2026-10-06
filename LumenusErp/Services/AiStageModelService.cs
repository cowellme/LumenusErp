using System.Security.Cryptography;
using System.Text;
using LumenusErp.Data;
using Microsoft.EntityFrameworkCore;

namespace LumenusErp.Services;

/// <summary>Строка страницы /admin/models. Model = null — по умолчанию.</summary>
public sealed record AiStageRow(AiStage Stage, string? Model, DateTime? UpdatedAt);

/// <summary>Модели аудио-этапов и дублей для myasi. UpdatedAt = MinValue, если ничего не задано.</summary>
public sealed record MyasiAiConfig(string? AsrModel, string? DedupModel, string? DiarizationModel, DateTime UpdatedAt);

/// <summary>Чистая логика ai-config и валидации.</summary>
public static class AiConfigRules
{
    public const int MaxModel = 200;

    /// <summary>Слабый ETag в кавычках: хеш от трёх моделей и времени изменения.</summary>
    public static string ETag(MyasiAiConfig c)
    {
        var raw = $"{c.AsrModel}|{c.DedupModel}|{c.DiarizationModel}|{c.UpdatedAt.Ticks}";
        return "\"" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)))[..16].ToLowerInvariant() + "\"";
    }

    /// <summary>true — If-None-Match совпал, отвечать 304.</summary>
    public static bool NotModified(string? ifNoneMatch, MyasiAiConfig c) =>
        UserPromptRules.NotModified(ifNoneMatch, ETag(c));

    /// <summary>Пустая строка → null; иначе обрезанная.</summary>
    public static string? Clean(string? model) => string.IsNullOrWhiteSpace(model) ? null : model.Trim();

    /// <summary>Ошибка валидации (по-русски) или null.</summary>
    public static string? Validate(string? stage, string? model)
    {
        if (AiStages.Find(stage) is null) return "Неизвестный этап.";
        if (Clean(model) is { Length: > MaxModel }) return $"Модель: не длиннее {MaxModel} символов.";
        return null;
    }
}

/// <summary>Чтение и сохранение моделей этапов: у этапов с промптом — в AiPrompts.Model, у остальных — в AiStageModels.</summary>
public sealed class AiStageModelService(IDbContextFactory<ApplicationDbContext> dbFactory, AiPromptStore promptStore)
{
    public async Task<List<AiStageRow>> GetAllAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var prompts = await db.AiPrompts.AsNoTracking().ToDictionaryAsync(p => p.Key, ct);
        var stages = await db.AiStageModels.AsNoTracking().ToDictionaryAsync(s => s.Stage, ct);
        return AiStages.All.Select(s =>
        {
            if (s.HasPrompt)
            {
                return prompts.TryGetValue(s.Key, out var p)
                    ? new AiStageRow(s, AiConfigRules.Clean(p.Model), p.UpdatedAt)
                    : new AiStageRow(s, null, null);
            }
            return stages.TryGetValue(s.Key, out var r) ? new AiStageRow(s, AiConfigRules.Clean(r.Model), r.UpdatedAt) : new AiStageRow(s, null, null);
        }).ToList();
    }

    /// <summary>Сохранить модель этапа; null/пусто — по умолчанию. Возвращает текст ошибки или null.</summary>
    public async Task<string?> SetAsync(string stage, string? model, CancellationToken ct = default)
    {
        var error = AiConfigRules.Validate(stage, model);
        if (error is not null) return error;
        var clean = AiConfigRules.Clean(model);
        var def = AiStages.Find(stage)!;

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (def.HasPrompt)
        {
            var row = await db.AiPrompts.FirstOrDefaultAsync(p => p.Key == stage, ct);
            if (row is null) return "Промпт этапа не найден в БД.";
            row.Model = clean;
            row.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            promptStore.Invalidate();
            return null;
        }

        var item = await db.AiStageModels.FirstOrDefaultAsync(s => s.Stage == stage, ct);
        if (item is null)
        {
            item = new AiStageModel { Stage = stage };
            db.AiStageModels.Add(item);
        }
        item.Model = clean;
        item.UpdatedAt = DateTime.UtcNow;
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: "23505" })
        {
            // Гонка двух сохранений: вторая вставка упёрлась в уникальный индекс — повторяем как обновление
            db.ChangeTracker.Clear();
            var existing = await db.AiStageModels.FirstAsync(s => s.Stage == stage, ct);
            existing.Model = clean;
            existing.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }
        return null;
    }

    public async Task<MyasiAiConfig> GetMyasiConfigAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var keys = new[] { AiStages.MyasiAsrKey, AiStages.MyasiDedupKey, AiStages.DiarizationKey };
        var rows = await db.AiStageModels.AsNoTracking().Where(s => keys.Contains(s.Stage)).ToListAsync(ct);
        string? Get(string k) => AiConfigRules.Clean(rows.FirstOrDefault(r => r.Stage == k)?.Model);
        var updated = rows.Count == 0 ? DateTime.MinValue : rows.Max(r => r.UpdatedAt);
        return new MyasiAiConfig(Get(AiStages.MyasiAsrKey), Get(AiStages.MyasiDedupKey), Get(AiStages.DiarizationKey), updated);
    }
}
