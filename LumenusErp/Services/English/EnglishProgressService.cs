using LumenusErp.Data;
using Microsoft.EntityFrameworkCore;

namespace LumenusErp.Services.English;

/// <summary>Сводка ученика для главной, прогресса и карточки у преподавателя.</summary>
public record EnglishStudentStats(
    int LessonsCompleted,
    int LessonsTotal,
    int MinutesThisMonth,
    int MinutesThisWeek,
    int Words,
    int AverageScore,
    int Xp,
    int Streak,
    int ActiveDaysThisMonth,
    Dictionary<DateOnly, int> MinutesByDay)
{
    public const int XpPerLevel = 250;
    public int Level => Xp / XpPerLevel + 1;
    public int XpInLevel => Xp % XpPerLevel;
}

/// <summary>Достижение: название, условие, иконка Bootstrap Icons и получено ли.</summary>
public record EnglishAchievement(string Title, string Description, string Icon, bool Done);

/// <summary>
/// Активность ученика (EnglishActivity) и всё, что из неё считается: XP, серия дней, тепловая карта, время с английским.
/// Дни считаются в часовом поясе профиля ученика.
/// </summary>
public class EnglishProgressService(IDbContextFactory<ApplicationDbContext> dbFactory)
{
    // Сколько минут и XP даёт действие
    public const int LessonMinutes = 30, LessonXp = 100;
    public const int TestMinutes = 5, TestXp = 50;
    public const int HomeworkMinutes = 20, HomeworkXp = 30;
    public const int WordMinutes = 1, WordXp = 5;

    /// <summary>Пишет активность и обновляет LastActivityAt; SaveChanges вызывает вызывающий код.</summary>
    public static async Task LogAsync(ApplicationDbContext db, string studentId, string kind, int minutes, int xp, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        db.EnglishActivities.Add(new EnglishActivity
        {
            Id = Guid.NewGuid(),
            StudentId = studentId,
            Kind = kind,
            Minutes = minutes,
            Xp = xp,
            CreatedAt = now,
        });
        var profile = await db.EnglishProfiles.FirstOrDefaultAsync(p => p.UserId == studentId, ct);
        if (profile is not null) profile.LastActivityAt = now;
    }

    /// <summary>Отметка «Знаю» на карточке слова.</summary>
    public async Task LogWordReviewAsync(string studentId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        await LogAsync(db, studentId, EnglishActivity.KindWords, WordMinutes, WordXp, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task<EnglishStudentStats> GetStatsAsync(string studentId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var profile = await db.EnglishProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == studentId, ct);
        var tz = EnglishRoles.FindTimeZone(profile?.TimeZone);
        var teacherId = profile?.TeacherId;

        var activity = await db.EnglishActivities.AsNoTracking()
            .Where(a => a.StudentId == studentId)
            .Select(a => new { a.CreatedAt, a.Minutes, a.Xp })
            .ToListAsync(ct);
        var byDay = activity
            .GroupBy(a => DateOnly.FromDateTime(EnglishRoles.ToLocal(a.CreatedAt, tz)))
            .ToDictionary(g => g.Key, g => g.Sum(a => a.Minutes));

        var today = DateOnly.FromDateTime(EnglishRoles.ToLocal(DateTime.UtcNow, tz));
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var weekStart = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));

        // Серия: подряд идущие активные дни, заканчивая сегодня или вчера
        var streak = 0;
        var day = byDay.ContainsKey(today) ? today : today.AddDays(-1);
        while (byDay.ContainsKey(day))
        {
            streak++;
            day = day.AddDays(-1);
        }

        var lessonsTotal = teacherId is null ? 0 : await db.EnglishLessons.CountAsync(l => l.TeacherId == teacherId && l.Published, ct);
        var lessonsCompleted = await db.EnglishLessonProgress.CountAsync(p => p.StudentId == studentId && p.CompletedAt != null, ct);
        var words = await db.EnglishWords.CountAsync(w => w.StudentId == studentId, ct);
        var scores = await db.EnglishTestResults.Where(r => r.StudentId == studentId).Select(r => r.Score).ToListAsync(ct);

        return new EnglishStudentStats(
            lessonsCompleted,
            lessonsTotal,
            byDay.Where(x => x.Key >= monthStart).Sum(x => x.Value),
            byDay.Where(x => x.Key >= weekStart).Sum(x => x.Value),
            words,
            scores.Count == 0 ? 0 : (int)Math.Round(scores.Average()),
            activity.Sum(a => a.Xp),
            streak,
            byDay.Count(x => x.Key >= monthStart),
            byDay);
    }

    public async Task<List<EnglishAchievement>> GetAchievementsAsync(string studentId, EnglishStudentStats stats, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var perfect = await db.EnglishTestResults.AnyAsync(r => r.StudentId == studentId && r.Score == 100, ct);
        var story = await db.EnglishSubmissions.AnyAsync(s => s.StudentId == studentId, ct);
        var activeDays = stats.MinutesByDay.Count;
        return
        [
            new("First chapter", "Первый завершённый урок", "bi-book", stats.LessonsCompleted > 0),
            new("On fire", "7 активных дней подряд", "bi-fire", stats.Streak >= 7),
            new("Word collector", "100 слов в словаре", "bi-translate", stats.Words >= 100),
            new("Perfect score", "Пройти тест без ошибок", "bi-bullseye", perfect),
            new("Storyteller", "Отправить первую домашнюю работу", "bi-mic", story),
            new("Consistency", "30 дней практики", "bi-trophy", activeDays >= 30),
        ];
    }

    /// <summary>Минуты по дням недели (Пн…Вс) за последние 7 дней.</summary>
    public static int[] LastWeek(EnglishStudentStats stats, DateOnly today)
    {
        var result = new int[7];
        for (var i = 0; i < 7; i++)
        {
            var d = today.AddDays(-6 + i);
            result[((int)d.DayOfWeek + 6) % 7] = stats.MinutesByDay.GetValueOrDefault(d);
        }
        return result;
    }

    /// <summary>Уровень клетки тепловой карты 0–4 по минутам за день.</summary>
    public static int HeatLevel(int minutes) => minutes switch
    {
        <= 0 => 0,
        < 10 => 1,
        < 25 => 2,
        < 45 => 3,
        _ => 4,
    };
}
