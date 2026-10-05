using LumenusErp.Data;
using Microsoft.EntityFrameworkCore;

namespace LumenusErp.Services.English;

/// <summary>Задание у ученика: последняя его работа (null — не сдавал).</summary>
public record EnglishHomeworkCard(EnglishHomework Homework, EnglishSubmission? LastSubmission);

/// <summary>Работа для проверки: с заданием и именем ученика.</summary>
public record EnglishSubmissionView(EnglishSubmission Submission, string StudentName);

/// <summary>
/// Домашние задания: назначение (всем ученикам или группе), сдача работ учениками и проверка с оценкой 0–100.
/// </summary>
public class EnglishHomeworkService(IDbContextFactory<ApplicationDbContext> dbFactory)
{
    public const int MaxTitle = 200, MaxInstruction = 5000, MaxAnswer = 20000, MaxComment = 5000;

    public async Task<List<EnglishHomework>> ListForTeacherAsync(string teacherId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.EnglishHomework.AsNoTracking().Include(h => h.Group).Include(h => h.Lesson).Include(h => h.Submissions)
            .Where(h => h.TeacherId == teacherId).OrderByDescending(h => h.CreatedAt).ToListAsync(ct);
    }

    /// <summary>Задания, адресованные ученику: всем ученикам преподавателя или группам ученика.</summary>
    public async Task<List<EnglishHomeworkCard>> ListForStudentAsync(string studentId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var teacherId = await EnglishProfileService.TeacherOfAsync(db, studentId, ct);
        if (teacherId is null) return new();
        var groups = await EnglishProfileService.GroupsOfAsync(db, studentId, ct);
        var homework = await db.EnglishHomework.AsNoTracking().Include(h => h.Lesson)
            .Where(h => h.TeacherId == teacherId && (h.GroupId == null || groups.Contains(h.GroupId.Value)))
            .OrderByDescending(h => h.CreatedAt).ToListAsync(ct);
        var ids = homework.Select(h => h.Id).ToList();
        var submissions = await db.EnglishSubmissions.AsNoTracking()
            .Where(s => s.StudentId == studentId && ids.Contains(s.HomeworkId))
            .OrderByDescending(s => s.SubmittedAt).ToListAsync(ct);
        return homework.Select(h => new EnglishHomeworkCard(h, submissions.FirstOrDefault(s => s.HomeworkId == h.Id))).ToList();
    }

    /// <summary>Создаёт или обновляет задание. dueLocal — срок в часовом поясе преподавателя.</summary>
    public async Task<EnglishResult> SaveAsync(string teacherId, EnglishHomework input, DateTime? dueLocal, TimeZoneInfo tz, CancellationToken ct = default)
    {
        var title = EnglishRoles.Clip(input.Title, MaxTitle);
        var instruction = EnglishRoles.Clip(input.Instruction, MaxInstruction);
        if (title.Length == 0 || instruction.Length == 0) return new("Укажите название и инструкцию.");

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (input.GroupId is { } groupId && !await db.EnglishGroups.AnyAsync(g => g.Id == groupId && g.TeacherId == teacherId, ct))
            return new("Группа не найдена.");
        if (input.LessonId is { } lessonId && !await db.EnglishLessons.AnyAsync(l => l.Id == lessonId && l.TeacherId == teacherId, ct))
            return new("Урок не найден.");

        EnglishHomework? h;
        if (input.Id == Guid.Empty)
        {
            h = new EnglishHomework { Id = Guid.NewGuid(), TeacherId = teacherId, CreatedAt = DateTime.UtcNow };
            db.EnglishHomework.Add(h);
        }
        else
        {
            h = await db.EnglishHomework.FirstOrDefaultAsync(x => x.Id == input.Id && x.TeacherId == teacherId, ct);
            if (h is null) return new("Задание не найдено.");
        }
        h.Title = title;
        h.Instruction = instruction;
        h.GroupId = input.GroupId;
        h.LessonId = input.LessonId;
        h.DueAt = dueLocal is { } d ? EnglishRoles.ToUtc(d, tz) : null;
        await db.SaveChangesAsync(ct);
        return EnglishResult.Success;
    }

    public async Task<bool> DeleteAsync(string teacherId, Guid id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.EnglishHomework.Where(h => h.Id == id && h.TeacherId == teacherId).ExecuteDeleteAsync(ct) > 0;
    }

    /// <summary>Сдача работы учеником; задание должно быть ему адресовано.</summary>
    public async Task<EnglishResult> SubmitAsync(string studentId, Guid homeworkId, string? text, CancellationToken ct = default)
    {
        var answer = (text ?? "").Trim();
        if (answer.Length == 0) return new("Напишите ответ.");
        if (answer.Length > MaxAnswer) return new($"Ответ длиннее {MaxAnswer} символов.");

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var teacherId = await EnglishProfileService.TeacherOfAsync(db, studentId, ct);
        var groups = await EnglishProfileService.GroupsOfAsync(db, studentId, ct);
        var allowed = teacherId is not null && await db.EnglishHomework.AnyAsync(h => h.Id == homeworkId && h.TeacherId == teacherId
            && (h.GroupId == null || groups.Contains(h.GroupId.Value)), ct);
        if (!allowed) return new("Задание не найдено.");

        var first = !await db.EnglishSubmissions.AnyAsync(s => s.StudentId == studentId && s.HomeworkId == homeworkId, ct);
        db.EnglishSubmissions.Add(new EnglishSubmission
        {
            Id = Guid.NewGuid(),
            HomeworkId = homeworkId,
            StudentId = studentId,
            Text = answer,
            Status = EnglishSubmission.StatusSubmitted,
            SubmittedAt = DateTime.UtcNow,
        });
        // XP только за первую сдачу задания, повторные отправки не накручивают
        if (first) await EnglishProgressService.LogAsync(db, studentId, EnglishActivity.KindHomework, EnglishProgressService.HomeworkMinutes, EnglishProgressService.HomeworkXp, ct);
        await db.SaveChangesAsync(ct);
        return EnglishResult.Success;
    }

    /// <summary>Работы учеников по заданиям преподавателя: сначала непроверенные.</summary>
    public async Task<List<EnglishSubmissionView>> ListSubmissionsAsync(string teacherId, string? studentId = null, bool onlyNew = false, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var q = db.EnglishSubmissions.AsNoTracking().Include(s => s.Homework)
            .Where(s => s.Homework!.TeacherId == teacherId);
        if (studentId is not null) q = q.Where(s => s.StudentId == studentId);
        if (onlyNew) q = q.Where(s => s.Status == EnglishSubmission.StatusSubmitted);
        var list = await q.OrderBy(s => s.Status == EnglishSubmission.StatusReviewed).ThenByDescending(s => s.SubmittedAt).Take(100).ToListAsync(ct);
        var ids = list.Select(s => s.StudentId).Distinct().ToList();
        var names = await db.EnglishProfiles.AsNoTracking().Include(p => p.User)
            .Where(p => ids.Contains(p.UserId)).ToDictionaryAsync(p => p.UserId, p => p.DisplayName, ct);
        return list.Select(s => new EnglishSubmissionView(s, names.GetValueOrDefault(s.StudentId, ""))).ToList();
    }

    public async Task<int> CountNewSubmissionsAsync(string teacherId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.EnglishSubmissions.CountAsync(s => s.Homework!.TeacherId == teacherId && s.Status == EnglishSubmission.StatusSubmitted, ct);
    }

    /// <summary>Отзыв преподавателя: комментарий и оценка 0–100; работа становится «проверено».</summary>
    public async Task<EnglishResult> ReviewAsync(string teacherId, Guid submissionId, int? score, string? comment, CancellationToken ct = default)
    {
        if (score is not (>= 0 and <= 100)) return new("Оценка от 0 до 100.");
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var s = await db.EnglishSubmissions.FirstOrDefaultAsync(x => x.Id == submissionId && x.Homework!.TeacherId == teacherId, ct);
        if (s is null) return new("Работа не найдена.");
        s.Score = score;
        s.Comment = EnglishRoles.Clip(comment, MaxComment);
        s.Status = EnglishSubmission.StatusReviewed;
        s.ReviewedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return EnglishResult.Success;
    }
}
