using LumenusErp.Data;
using Microsoft.EntityFrameworkCore;

namespace LumenusErp.Services.English;

/// <summary>Урок в списке с прогрессом текущего ученика (у преподавателя прогресс 0).</summary>
public record EnglishLessonCard(EnglishLesson Lesson, int Unit, int Percent, bool Completed);

/// <summary>
/// Учебные программы, уроки с блоками и прохождение уроков учениками.
/// Преподаватель видит и правит свои уроки; ученик — только опубликованные уроки своего преподавателя.
/// </summary>
public class EnglishLessonService(IDbContextFactory<ApplicationDbContext> dbFactory, MediaService media)
{
    public const int MaxTitle = 200, MaxSummary = 1000, MaxBlock = 20000, MaxBlocks = 50;

    // ── Программы ───────────────────────────────────────────────────

    public async Task<List<(EnglishProgram Program, int Lessons)>> ListProgramsAsync(string teacherId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var programs = await db.EnglishPrograms.AsNoTracking().Where(p => p.TeacherId == teacherId).OrderBy(p => p.Title).ToListAsync(ct);
        var counts = await db.EnglishLessons.Where(l => l.TeacherId == teacherId && l.ProgramId != null)
            .GroupBy(l => l.ProgramId!.Value).Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        return programs.Select(p => (p, counts.GetValueOrDefault(p.Id))).ToList();
    }

    public async Task<EnglishResult> SaveProgramAsync(string teacherId, EnglishProgram input, CancellationToken ct = default)
    {
        var title = EnglishRoles.Clip(input.Title, MaxTitle);
        if (title.Length == 0) return new("Укажите название программы.");
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        EnglishProgram? p;
        if (input.Id == Guid.Empty)
        {
            p = new EnglishProgram { Id = Guid.NewGuid(), TeacherId = teacherId, CreatedAt = DateTime.UtcNow };
            db.EnglishPrograms.Add(p);
        }
        else
        {
            p = await db.EnglishPrograms.FirstOrDefaultAsync(x => x.Id == input.Id && x.TeacherId == teacherId, ct);
            if (p is null) return new("Программа не найдена.");
        }
        p.Title = title;
        p.Level = EnglishRoles.Clip(input.Level, 50);
        p.Description = EnglishRoles.Clip(input.Description, 1000);
        await db.SaveChangesAsync(ct);
        return EnglishResult.Success;
    }

    public async Task<bool> DeleteProgramAsync(string teacherId, Guid id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.EnglishPrograms.Where(p => p.Id == id && p.TeacherId == teacherId).ExecuteDeleteAsync(ct) > 0;
    }

    // ── Уроки ───────────────────────────────────────────────────────

    /// <summary>Уроки преподавателя (все, включая черновики).</summary>
    public async Task<List<EnglishLessonCard>> ListForTeacherAsync(string teacherId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var lessons = await db.EnglishLessons.AsNoTracking().Include(l => l.Program)
            .Where(l => l.TeacherId == teacherId)
            .OrderBy(l => l.Order).ThenBy(l => l.CreatedAt)
            .ToListAsync(ct);
        return lessons.Select((l, i) => new EnglishLessonCard(l, i + 1, 0, false)).ToList();
    }

    /// <summary>Опубликованные уроки преподавателя ученика с его прогрессом.</summary>
    public async Task<List<EnglishLessonCard>> ListForStudentAsync(string studentId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var teacherId = await EnglishProfileService.TeacherOfAsync(db, studentId, ct);
        if (teacherId is null) return new();
        var lessons = await db.EnglishLessons.AsNoTracking().Include(l => l.Program)
            .Where(l => l.TeacherId == teacherId && l.Published)
            .OrderBy(l => l.Order).ThenBy(l => l.CreatedAt)
            .ToListAsync(ct);
        var progress = await db.EnglishLessonProgress.AsNoTracking()
            .Where(p => p.StudentId == studentId)
            .ToDictionaryAsync(p => p.LessonId, ct);
        return lessons.Select((l, i) =>
        {
            var p = progress.GetValueOrDefault(l.Id);
            return new EnglishLessonCard(l, i + 1, p?.CompletedAt is not null ? 100 : p?.Percent ?? 0, p?.CompletedAt is not null);
        }).ToList();
    }

    /// <summary>Урок с блоками, если пользователь имеет к нему доступ (автор или ученик автора, урок опубликован).</summary>
    public async Task<EnglishLesson?> GetAsync(string userId, Guid id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var lesson = await db.EnglishLessons.AsNoTracking().Include(l => l.Program)
            .Include(l => l.Blocks.OrderBy(b => b.Order))
            .FirstOrDefaultAsync(l => l.Id == id, ct);
        if (lesson is null) return null;
        if (lesson.TeacherId == userId) return lesson;
        return lesson.Published && await EnglishProfileService.IsStudentOfAsync(db, lesson.TeacherId, userId, ct) ? lesson : null;
    }

    /// <summary>Номер следующего юнита у преподавателя.</summary>
    public async Task<int> NextOrderAsync(string teacherId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return (await db.EnglishLessons.Where(l => l.TeacherId == teacherId).MaxAsync(l => (int?)l.Order, ct) ?? 0) + 1;
    }

    /// <summary>Создаёт или обновляет урок вместе с блоками (блоки заменяются целиком). Возвращает Id урока.</summary>
    public async Task<(Guid? Id, string? Error)> SaveAsync(string teacherId, EnglishLesson input, List<EnglishLessonBlock> blocks, CancellationToken ct = default)
    {
        var title = EnglishRoles.Clip(input.Title, MaxTitle);
        if (title.Length == 0) return (null, "Укажите название урока.");
        var cleanBlocks = blocks
            .Where(b => !string.IsNullOrWhiteSpace(b.Text))
            .Take(MaxBlocks)
            .Select((b, i) => new EnglishLessonBlock
            {
                Id = Guid.NewGuid(),
                Order = i,
                Type = EnglishLessonBlock.Types.Contains(b.Type) ? b.Type : EnglishLessonBlock.Types[0],
                Text = b.Text.Length > MaxBlock ? b.Text[..MaxBlock] : b.Text,
            })
            .ToList();
        if (input.Published && cleanBlocks.Count == 0) return (null, "Чтобы опубликовать урок, добавьте хотя бы один блок с текстом.");
        var videoUrl = EnglishRoles.Clip(input.VideoUrl, 1000);
        if (videoUrl.Length > 0 && !IsHttpUrl(videoUrl)) return (null, "Ссылка на запись должна начинаться с http:// или https://.");

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (input.ProgramId is { } programId && !await db.EnglishPrograms.AnyAsync(p => p.Id == programId && p.TeacherId == teacherId, ct))
        {
            return (null, "Программа не найдена.");
        }

        EnglishLesson? lesson;
        var now = DateTime.UtcNow;
        if (input.Id == Guid.Empty)
        {
            lesson = new EnglishLesson { Id = Guid.NewGuid(), TeacherId = teacherId, CreatedAt = now };
            db.EnglishLessons.Add(lesson);
        }
        else
        {
            lesson = await db.EnglishLessons.Include(l => l.Blocks).FirstOrDefaultAsync(l => l.Id == input.Id && l.TeacherId == teacherId, ct);
            if (lesson is null) return (null, "Урок не найден.");
            db.EnglishLessonBlocks.RemoveRange(lesson.Blocks);
        }

        lesson.Title = title;
        lesson.Summary = EnglishRoles.Clip(input.Summary, MaxSummary);
        lesson.Topic = EnglishRoles.Clip(input.Topic, 50);
        lesson.Level = EnglishRoles.Clip(input.Level, 50);
        lesson.DurationMinutes = Math.Clamp(input.DurationMinutes, 5, 300);
        lesson.Color = EnglishLesson.Colors.Contains(input.Color) ? input.Color : EnglishLesson.Colors[0];
        lesson.VideoUrl = videoUrl;
        lesson.ProgramId = input.ProgramId;
        lesson.Order = input.Order;
        lesson.Published = input.Published;
        lesson.UpdatedAt = now;
        foreach (var b in cleanBlocks)
        {
            b.LessonId = lesson.Id;
            db.EnglishLessonBlocks.Add(b);
        }
        await db.SaveChangesAsync(ct);
        return (lesson.Id, null);
    }

    /// <summary>Заменяет фото доски/конспекта; старый файл удаляется.</summary>
    public async Task<EnglishResult> SetPhotoAsync(string teacherId, Guid lessonId, Stream content, string fileName, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var lesson = await db.EnglishLessons.FirstOrDefaultAsync(l => l.Id == lessonId && l.TeacherId == teacherId, ct);
        if (lesson is null) return new("Урок не найден.");
        MediaFile file;
        try
        {
            file = await media.SaveAsync(content, fileName, ct);
        }
        catch (MediaException ex)
        {
            return new(ex.Message);
        }
        var old = lesson.PhotoMediaId;
        lesson.PhotoMediaId = file.Id;
        lesson.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        if (old is { } oldId) await media.DeleteIfUnreferencedAsync([oldId]);
        return EnglishResult.Success;
    }

    public async Task RemovePhotoAsync(string teacherId, Guid lessonId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var lesson = await db.EnglishLessons.FirstOrDefaultAsync(l => l.Id == lessonId && l.TeacherId == teacherId, ct);
        if (lesson?.PhotoMediaId is not { } old) return;
        lesson.PhotoMediaId = null;
        await db.SaveChangesAsync(ct);
        await media.DeleteIfUnreferencedAsync([old]);
    }

    public async Task<bool> DeleteAsync(string teacherId, Guid id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var lesson = await db.EnglishLessons.FirstOrDefaultAsync(l => l.Id == id && l.TeacherId == teacherId, ct);
        if (lesson is null) return false;
        var photo = lesson.PhotoMediaId;
        db.EnglishLessons.Remove(lesson);
        await db.SaveChangesAsync(ct);
        if (photo is { } p) await media.DeleteIfUnreferencedAsync([p]);
        return true;
    }

    // ── Прохождение ─────────────────────────────────────────────────

    public async Task<EnglishLessonProgress?> GetProgressAsync(string studentId, Guid lessonId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.EnglishLessonProgress.AsNoTracking().FirstOrDefaultAsync(p => p.StudentId == studentId && p.LessonId == lessonId, ct);
    }

    /// <summary>Заметки ученика к уроку.</summary>
    public async Task SaveNotesAsync(string studentId, Guid lessonId, string? notes, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var p = await GetOrAddProgressAsync(db, studentId, lessonId, ct);
        if (p is null) return;
        p.Notes = notes is { Length: > 10000 } ? notes[..10000] : notes ?? "";
        if (p.Percent == 0) p.Percent = 10;
        p.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Отмечает, что ученик открыл урок (прогресс не меньше 10%).</summary>
    public async Task MarkOpenedAsync(string studentId, Guid lessonId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var p = await GetOrAddProgressAsync(db, studentId, lessonId, ct);
        if (p is null || p.Percent >= 10) return;
        p.Percent = 10;
        p.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Завершает урок: 100%, активность (+XP) только при первом завершении.</summary>
    public async Task CompleteAsync(string studentId, Guid lessonId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var p = await GetOrAddProgressAsync(db, studentId, lessonId, ct);
        if (p is null || p.CompletedAt is not null) return;
        p.Percent = 100;
        p.CompletedAt = p.UpdatedAt = DateTime.UtcNow;
        await EnglishProgressService.LogAsync(db, studentId, EnglishActivity.KindLesson, EnglishProgressService.LessonMinutes, EnglishProgressService.LessonXp, ct);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Рефлексия после урока: сохраняется в прогрессе и уходит сообщением преподавателю.</summary>
    public async Task SaveReflectionAsync(string studentId, Guid lessonId, string reflection, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var p = await GetOrAddProgressAsync(db, studentId, lessonId, ct);
        if (p is null) return;
        p.Reflection = EnglishRoles.Clip(reflection, 100);
        p.UpdatedAt = DateTime.UtcNow;
        var lesson = await db.EnglishLessons.AsNoTracking().FirstAsync(l => l.Id == lessonId, ct);
        db.EnglishMessages.Add(new EnglishMessage
        {
            Id = Guid.NewGuid(),
            FromId = studentId,
            ToId = lesson.TeacherId,
            Text = $"Урок «{lesson.Title}»: {p.Reflection}",
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(ct);
    }

    private static async Task<EnglishLessonProgress?> GetOrAddProgressAsync(ApplicationDbContext db, string studentId, Guid lessonId, CancellationToken ct)
    {
        var p = await db.EnglishLessonProgress.FirstOrDefaultAsync(x => x.StudentId == studentId && x.LessonId == lessonId, ct);
        if (p is not null) return p;
        var lesson = await db.EnglishLessons.AsNoTracking().FirstOrDefaultAsync(l => l.Id == lessonId && l.Published, ct);
        if (lesson is null || !await EnglishProfileService.IsStudentOfAsync(db, lesson.TeacherId, studentId, ct)) return null;
        p = new EnglishLessonProgress { StudentId = studentId, LessonId = lessonId, UpdatedAt = DateTime.UtcNow };
        db.EnglishLessonProgress.Add(p);
        return p;
    }

    /// <summary>Как показать запись: YouTube — iframe, прямой файл (.mp4/.webm/.ogg/.mov) — video, иначе ссылкой.</summary>
    public static (string Kind, string Src) VideoEmbed(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var u)) return ("none", "");
        var host = u.Host.ToLowerInvariant();
        string? youtubeId = null;
        if (host is "youtu.be") youtubeId = u.AbsolutePath.Trim('/');
        else if (host.EndsWith("youtube.com"))
        {
            youtubeId = u.AbsolutePath.StartsWith("/embed/") || u.AbsolutePath.StartsWith("/shorts/")
                ? u.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries).ElementAtOrDefault(1)
                : System.Web.HttpUtility.ParseQueryString(u.Query)["v"];
        }
        if (!string.IsNullOrEmpty(youtubeId) && youtubeId.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_'))
        {
            return ("youtube", "https://www.youtube-nocookie.com/embed/" + youtubeId);
        }
        var ext = Path.GetExtension(u.AbsolutePath).ToLowerInvariant();
        return ext is ".mp4" or ".webm" or ".ogg" or ".mov" ? ("video", url) : ("link", url);
    }

    /// <summary>Строки блока «Новые слова» вида «word — перевод».</summary>
    public static List<(string Word, string Translation)> ParseWords(string text) =>
        text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line =>
            {
                var parts = line.Split([" — ", " – ", " - ", "\t"], 2, StringSplitOptions.TrimEntries);
                return (parts[0], parts.Length > 1 ? parts[1] : "");
            })
            .Where(x => x.Item1.Length > 0)
            .ToList();

    public static bool IsHttpUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var u) && (u.Scheme == Uri.UriSchemeHttp || u.Scheme == Uri.UriSchemeHttps);
}
