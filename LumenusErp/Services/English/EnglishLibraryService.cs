using LumenusErp.Data;
using Microsoft.EntityFrameworkCore;

namespace LumenusErp.Services.English;

/// <summary>Материал библиотеки с отметкой «в избранном» у текущего ученика.</summary>
public record EnglishMaterialCard(EnglishMaterial Material, bool Favorite);

/// <summary>Личный словарь ученика и библиотека материалов преподавателя (с избранным у учеников).</summary>
public class EnglishLibraryService(IDbContextFactory<ApplicationDbContext> dbFactory)
{
    public const int MaxWords = 5000;

    // ── Словарь ─────────────────────────────────────────────────────

    public async Task<List<EnglishWord>> ListWordsAsync(string studentId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.EnglishWords.AsNoTracking().Where(w => w.StudentId == studentId)
            .OrderBy(w => w.Learned).ThenByDescending(w => w.CreatedAt).ToListAsync(ct);
    }

    public async Task<EnglishResult> AddWordAsync(string studentId, string? word, string? translation, string? example, CancellationToken ct = default)
    {
        var w = EnglishRoles.Clip(word, 200);
        var t = EnglishRoles.Clip(translation, 300);
        if (w.Length == 0 || t.Length == 0) return new("Укажите слово и перевод.");

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (await db.EnglishWords.CountAsync(x => x.StudentId == studentId, ct) >= MaxWords) return new($"В словаре уже {MaxWords} слов.");
        var lower = w.ToLower();
        if (await db.EnglishWords.AnyAsync(x => x.StudentId == studentId && x.Word.ToLower() == lower, ct)) return new("Это слово уже есть в словаре.");
        db.EnglishWords.Add(new EnglishWord
        {
            Id = Guid.NewGuid(),
            StudentId = studentId,
            Word = w,
            Translation = t,
            Example = EnglishRoles.Clip(example, 1000),
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(ct);
        return EnglishResult.Success;
    }

    public async Task SetLearnedAsync(string studentId, Guid id, bool learned, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        await db.EnglishWords.Where(w => w.Id == id && w.StudentId == studentId)
            .ExecuteUpdateAsync(s => s.SetProperty(w => w.Learned, learned), ct);
    }

    public async Task DeleteWordAsync(string studentId, Guid id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        await db.EnglishWords.Where(w => w.Id == id && w.StudentId == studentId).ExecuteDeleteAsync(ct);
    }

    // ── Материалы ───────────────────────────────────────────────────

    /// <summary>Материалы для пользователя: свои у преподавателя, материалы преподавателя у ученика.</summary>
    public async Task<List<EnglishMaterialCard>> ListMaterialsAsync(string userId, bool isTeacher, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var ownerId = isTeacher ? userId : await EnglishProfileService.TeacherOfAsync(db, userId, ct);
        if (ownerId is null) return new();
        var materials = await db.EnglishMaterials.AsNoTracking().Where(m => m.TeacherId == ownerId)
            .OrderBy(m => m.Category).ThenBy(m => m.Title).ToListAsync(ct);
        var favorites = isTeacher
            ? new HashSet<Guid>()
            : (await db.EnglishFavorites.Where(f => f.StudentId == userId).Select(f => f.MaterialId).ToListAsync(ct)).ToHashSet();
        return materials.Select(m => new EnglishMaterialCard(m, favorites.Contains(m.Id))).ToList();
    }

    public async Task<EnglishResult> SaveMaterialAsync(string teacherId, EnglishMaterial input, CancellationToken ct = default)
    {
        var title = EnglishRoles.Clip(input.Title, 200);
        if (title.Length == 0) return new("Укажите название материала.");
        var url = EnglishRoles.Clip(input.Url, 1000);
        if (url.Length > 0 && !EnglishLessonService.IsHttpUrl(url)) return new("Ссылка должна начинаться с http:// или https://.");

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        EnglishMaterial? m;
        if (input.Id == Guid.Empty)
        {
            m = new EnglishMaterial { Id = Guid.NewGuid(), TeacherId = teacherId, CreatedAt = DateTime.UtcNow };
            db.EnglishMaterials.Add(m);
        }
        else
        {
            m = await db.EnglishMaterials.FirstOrDefaultAsync(x => x.Id == input.Id && x.TeacherId == teacherId, ct);
            if (m is null) return new("Материал не найден.");
        }
        m.Title = title;
        m.Category = EnglishMaterial.Categories.Contains(input.Category) ? input.Category : EnglishMaterial.Categories[0];
        m.Level = EnglishRoles.Clip(input.Level, 50);
        m.Description = EnglishRoles.Clip(input.Description, 1000);
        m.Content = EnglishRoles.Clip(input.Content, 20000);
        m.Url = url;
        await db.SaveChangesAsync(ct);
        return EnglishResult.Success;
    }

    public async Task<bool> DeleteMaterialAsync(string teacherId, Guid id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.EnglishMaterials.Where(m => m.Id == id && m.TeacherId == teacherId).ExecuteDeleteAsync(ct) > 0;
    }

    public async Task ToggleFavoriteAsync(string studentId, Guid materialId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var deleted = await db.EnglishFavorites.Where(f => f.StudentId == studentId && f.MaterialId == materialId).ExecuteDeleteAsync(ct);
        if (deleted > 0) return;
        var teacherId = await EnglishProfileService.TeacherOfAsync(db, studentId, ct);
        if (!await db.EnglishMaterials.AnyAsync(m => m.Id == materialId && m.TeacherId == teacherId, ct)) return;
        db.EnglishFavorites.Add(new EnglishFavorite { StudentId = studentId, MaterialId = materialId });
        await db.SaveChangesAsync(ct);
    }
}
