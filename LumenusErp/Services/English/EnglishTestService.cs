using LumenusErp.Data;
using Microsoft.EntityFrameworkCore;

namespace LumenusErp.Services.English;

/// <summary>Тест в списке: последний результат ученика (null — не проходил).</summary>
public record EnglishTestCard(EnglishTest Test, int Questions, EnglishTestResult? LastResult);

/// <summary>Результат теста для проверки преподавателем: тест с вопросами и ученик.</summary>
public record EnglishResultView(EnglishTestResult Result, EnglishTest Test, string StudentName);

/// <summary>Тесты с автопроверкой: конструктор у преподавателя, прохождение и результаты у ученика.</summary>
public class EnglishTestService(IDbContextFactory<ApplicationDbContext> dbFactory)
{
    public const int MaxQuestions = 50, MaxOptions = 8, MaxOption = 300;

    public async Task<List<EnglishTestCard>> ListForTeacherAsync(string teacherId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var tests = await db.EnglishTests.AsNoTracking().Include(t => t.Questions).Include(t => t.Lesson)
            .Where(t => t.TeacherId == teacherId).OrderByDescending(t => t.CreatedAt).ToListAsync(ct);
        return tests.Select(t => new EnglishTestCard(t, t.Questions.Count, null)).ToList();
    }

    public async Task<List<EnglishTestCard>> ListForStudentAsync(string studentId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var teacherId = await EnglishProfileService.TeacherOfAsync(db, studentId, ct);
        if (teacherId is null) return new();
        var tests = await db.EnglishTests.AsNoTracking().Include(t => t.Questions).Include(t => t.Lesson)
            .Where(t => t.TeacherId == teacherId && t.Published)
            .OrderByDescending(t => t.Required).ThenByDescending(t => t.CreatedAt).ToListAsync(ct);
        var ids = tests.Select(t => t.Id).ToList();
        var results = await db.EnglishTestResults.AsNoTracking()
            .Where(r => r.StudentId == studentId && ids.Contains(r.TestId))
            .OrderByDescending(r => r.CreatedAt).ToListAsync(ct);
        return tests.Select(t => new EnglishTestCard(t, t.Questions.Count, results.FirstOrDefault(r => r.TestId == t.Id))).ToList();
    }

    /// <summary>Тест с вопросами для автора или ученика автора (тест опубликован).</summary>
    public async Task<EnglishTest?> GetAsync(string userId, Guid id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var test = await db.EnglishTests.AsNoTracking().Include(t => t.Questions.OrderBy(q => q.Order)).Include(t => t.Lesson)
            .FirstOrDefaultAsync(t => t.Id == id, ct);
        if (test is null) return null;
        if (test.TeacherId == userId) return test;
        return test.Published && await EnglishProfileService.IsStudentOfAsync(db, test.TeacherId, userId, ct) ? test : null;
    }

    /// <summary>Тест урока (первый опубликованный), чтобы предложить его на странице урока.</summary>
    public async Task<Guid?> FirstForLessonAsync(Guid lessonId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.EnglishTests.Where(t => t.LessonId == lessonId && t.Published).OrderBy(t => t.CreatedAt).Select(t => (Guid?)t.Id).FirstOrDefaultAsync(ct);
    }

    /// <summary>Проверка вопросов конструктора: текст ошибки или null.</summary>
    public static string? Validate(string title, List<EnglishQuestion> questions)
    {
        if (string.IsNullOrWhiteSpace(title)) return "Укажите название теста.";
        if (questions.Count == 0) return "Добавьте хотя бы один вопрос.";
        if (questions.Count > MaxQuestions) return $"Не больше {MaxQuestions} вопросов.";
        for (var i = 0; i < questions.Count; i++)
        {
            var q = questions[i];
            var options = q.Options.Select(o => o.Trim()).ToList();
            if (string.IsNullOrWhiteSpace(q.Text)) return $"Вопрос {i + 1}: заполните текст.";
            if (options.Count is < 2 or > MaxOptions || options.Any(o => o.Length is 0 or > MaxOption))
                return $"Вопрос {i + 1}: нужно от 2 до {MaxOptions} непустых вариантов.";
            if (options.Distinct().Count() != options.Count) return $"Вопрос {i + 1}: варианты не должны повторяться.";
            if (!options.Contains(q.Correct.Trim())) return $"Вопрос {i + 1}: правильный ответ должен совпадать с одним из вариантов.";
        }
        return null;
    }

    /// <summary>Создаёт или обновляет тест; вопросы заменяются целиком.</summary>
    public async Task<(Guid? Id, string? Error)> SaveAsync(string teacherId, EnglishTest input, List<EnglishQuestion> questions, CancellationToken ct = default)
    {
        var title = EnglishRoles.Clip(input.Title, 200);
        var error = Validate(title, questions);
        if (error is not null) return (null, error);

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (input.LessonId is { } lessonId && !await db.EnglishLessons.AnyAsync(l => l.Id == lessonId && l.TeacherId == teacherId, ct))
        {
            return (null, "Урок не найден.");
        }

        EnglishTest? test;
        var now = DateTime.UtcNow;
        if (input.Id == Guid.Empty)
        {
            test = new EnglishTest { Id = Guid.NewGuid(), TeacherId = teacherId, CreatedAt = now };
            db.EnglishTests.Add(test);
        }
        else
        {
            test = await db.EnglishTests.Include(t => t.Questions).FirstOrDefaultAsync(t => t.Id == input.Id && t.TeacherId == teacherId, ct);
            if (test is null) return (null, "Тест не найден.");
            db.EnglishQuestions.RemoveRange(test.Questions);
        }
        test.Title = title;
        test.Description = EnglishRoles.Clip(input.Description, 1000);
        test.LessonId = input.LessonId;
        test.Required = input.Required;
        test.Published = input.Published;
        test.UpdatedAt = now;
        for (var i = 0; i < questions.Count; i++)
        {
            var q = questions[i];
            db.EnglishQuestions.Add(new EnglishQuestion
            {
                Id = Guid.NewGuid(),
                TestId = test.Id,
                Order = i,
                Text = EnglishRoles.Clip(q.Text, 1000),
                Options = q.Options.Select(o => o.Trim()).ToList(),
                Correct = q.Correct.Trim(),
                Explanation = EnglishRoles.Clip(q.Explanation, 1000),
            });
        }
        await db.SaveChangesAsync(ct);
        return (test.Id, null);
    }

    public async Task<bool> DeleteAsync(string teacherId, Guid id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.EnglishTests.Where(t => t.Id == id && t.TeacherId == teacherId).ExecuteDeleteAsync(ct) > 0;
    }

    /// <summary>Может ли ученик пройти тест ещё раз (повторные попытки разрешает преподаватель).</summary>
    public async Task<bool> CanTakeAsync(string studentId, Guid testId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var allowRetakes = await db.EnglishProfiles.Where(p => p.UserId == studentId).Select(p => p.AllowRetakes).FirstOrDefaultAsync(ct);
        return allowRetakes || !await db.EnglishTestResults.AnyAsync(r => r.StudentId == studentId && r.TestId == testId, ct);
    }

    /// <summary>Проверяет ответы и сохраняет результат. null — теста нет или попытка запрещена.</summary>
    public async Task<EnglishTestResult?> SubmitAsync(string studentId, Guid testId, List<string> answers, CancellationToken ct = default)
    {
        var test = await GetAsync(studentId, testId, ct);
        if (test is null || test.TeacherId == studentId || test.Questions.Count == 0) return null;
        if (!await CanTakeAsync(studentId, testId, ct)) return null;

        var normalized = test.Questions.Select((q, i) => i < answers.Count ? EnglishRoles.Clip(answers[i], MaxOption) : "").ToList();
        var correct = test.Questions.Where((q, i) => normalized[i] == q.Correct).Count();
        var result = new EnglishTestResult
        {
            Id = Guid.NewGuid(),
            TestId = testId,
            StudentId = studentId,
            CorrectCount = correct,
            Total = test.Questions.Count,
            Score = (int)Math.Round(correct * 100.0 / test.Questions.Count),
            Answers = normalized,
            CreatedAt = DateTime.UtcNow,
        };
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        db.EnglishTestResults.Add(result);
        await EnglishProgressService.LogAsync(db, studentId, EnglishActivity.KindTest, EnglishProgressService.TestMinutes, EnglishProgressService.TestXp, ct);
        await db.SaveChangesAsync(ct);
        return result;
    }

    /// <summary>Результаты ученика, новые сверху (с тестом и вопросами).</summary>
    public async Task<List<EnglishTestResult>> ResultsOfStudentAsync(string studentId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.EnglishTestResults.AsNoTracking().Include(r => r.Test)
            .Where(r => r.StudentId == studentId).OrderByDescending(r => r.CreatedAt).ToListAsync(ct);
    }

    /// <summary>Последние результаты учеников по тестам преподавателя (для страницы проверки).</summary>
    public async Task<List<EnglishResultView>> RecentResultsAsync(string teacherId, string? studentId = null, int take = 30, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var q = db.EnglishTestResults.AsNoTracking()
            .Include(r => r.Test!).ThenInclude(t => t.Questions)
            .Where(r => r.Test!.TeacherId == teacherId);
        if (studentId is not null) q = q.Where(r => r.StudentId == studentId);
        var results = await q.OrderByDescending(r => r.CreatedAt).Take(take).ToListAsync(ct);
        var ids = results.Select(r => r.StudentId).Distinct().ToList();
        var names = await db.EnglishProfiles.AsNoTracking().Include(p => p.User)
            .Where(p => ids.Contains(p.UserId)).ToDictionaryAsync(p => p.UserId, p => p.DisplayName, ct);
        return results.Select(r =>
        {
            r.Test!.Questions = r.Test.Questions.OrderBy(x => x.Order).ToList();
            return new EnglishResultView(r, r.Test, names.GetValueOrDefault(r.StudentId, ""));
        }).ToList();
    }
}
