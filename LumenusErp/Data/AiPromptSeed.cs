using LumenusErp.Services;
using Microsoft.EntityFrameworkCore;

namespace LumenusErp.Data;

/// <summary>Начальные промпты ИИ. Добавляет только отсутствующие ключи, существующие строки не трогает.</summary>
public static class AiPromptSeed
{
    public static async Task EnsureSeededAsync(ApplicationDbContext db)
    {
        var existing = await db.AiPrompts.Select(p => p.Key).ToListAsync();
        var now = DateTime.UtcNow;

        if (!existing.Contains(DefaultPrompts.EstimateKey))
        {
            db.AiPrompts.Add(new AiPrompt
            {
                Key = DefaultPrompts.EstimateKey,
                Title = DefaultPrompts.EstimateTitle,
                SystemPrompt = DefaultPrompts.Estimate,
                UpdatedAt = now,
            });
        }

        if (!existing.Contains(DefaultPrompts.FaqKey))
        {
            db.AiPrompts.Add(new AiPrompt
            {
                Key = DefaultPrompts.FaqKey,
                Title = DefaultPrompts.FaqTitle,
                SystemPrompt = DefaultPrompts.Faq,
                UpdatedAt = now,
            });
        }

        if (!existing.Contains(DefaultPrompts.CallTasksKey))
        {
            db.AiPrompts.Add(new AiPrompt
            {
                Key = DefaultPrompts.CallTasksKey,
                Title = DefaultPrompts.CallTasksTitle,
                SystemPrompt = DefaultPrompts.CallTasks,
                UpdatedAt = now,
            });
        }

        await db.SaveChangesAsync();
    }
}
