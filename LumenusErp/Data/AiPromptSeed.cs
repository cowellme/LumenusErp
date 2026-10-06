using LumenusErp.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LumenusErp.Data;

/// <summary>
/// Начальные промпты ИИ. Добавляет только отсутствующие ключи, существующие строки не трогает —
/// кроме call-tasks: если там всё ещё прежний текст по умолчанию (CallTasksV1), он заменяется новым.
/// </summary>
public static class AiPromptSeed
{
    /// <summary>Совпадает ли сохранённый текст с прежним умолчанием (до trim и \r\n → \n): тогда админ его не правил и его можно заменить.</summary>
    public static bool ShouldUpgrade(string? stored, string previousDefault) =>
        stored is not null && Normalize(stored) == Normalize(previousDefault);

    private static string Normalize(string s) => s.Replace("\r\n", "\n").Trim();

    public static async Task EnsureSeededAsync(ApplicationDbContext db, ILogger? log = null)
    {
        var existing = await db.AiPrompts.Select(p => p.Key).ToListAsync();
        var now = DateTime.UtcNow;

        if (existing.Contains(DefaultPrompts.CallTasksKey))
        {
            var row = await db.AiPrompts.FirstAsync(p => p.Key == DefaultPrompts.CallTasksKey);
            if (ShouldUpgrade(row.SystemPrompt, DefaultPrompts.CallTasksV1))
            {
                row.SystemPrompt = DefaultPrompts.CallTasks;
                row.UpdatedAt = now;
                log?.LogInformation("Промпт {Key} обновлён до версии со сроками (start/deadline)", row.Key);
            }
            else if (!ShouldUpgrade(row.SystemPrompt, DefaultPrompts.CallTasks))
            {
                log?.LogWarning("Промпт {Key} изменён вручную и не обновлён: в новом формате ответа есть поля start/deadline, добавьте их в /admin/prompts (текст по умолчанию — «Сбросить к умолчанию»)", row.Key);
            }
        }

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

        if (!existing.Contains(DefaultPrompts.MyasiTasksKey))
        {
            db.AiPrompts.Add(new AiPrompt
            {
                Key = DefaultPrompts.MyasiTasksKey,
                Title = DefaultPrompts.MyasiTasksTitle,
                SystemPrompt = DefaultPrompts.MyasiTasks,
                UpdatedAt = now,
            });
        }

        await db.SaveChangesAsync();
    }
}
