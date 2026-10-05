using LumenusErp.Data;
using Microsoft.EntityFrameworkCore;

namespace LumenusErp.Services.English;

/// <summary>Пункт календаря: событие или срок домашнего задания. Время — UTC. CanEdit — событие создал текущий пользователь.</summary>
public record EnglishCalendarItem(Guid? EventId, string Title, string Kind, DateTime StartsAt, int DurationMinutes, string Link, string Notes, string Audience, bool CanEdit);

/// <summary>
/// Расписание: события преподавателя (ученику, группе или всем ученикам), личные события ученика и сроки домашних заданий.
/// </summary>
public class EnglishScheduleService(IDbContextFactory<ApplicationDbContext> dbFactory)
{
    /// <summary>События и дедлайны пользователя в интервале [fromUtc, toUtc).</summary>
    public async Task<List<EnglishCalendarItem>> ListAsync(string userId, bool isTeacher, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var items = new List<EnglishCalendarItem>();

        if (isTeacher)
        {
            var events = await db.EnglishEvents.AsNoTracking().Include(e => e.Group)
                .Where(e => e.OwnerId == userId && e.StartsAt >= fromUtc && e.StartsAt < toUtc).ToListAsync(ct);
            var studentIds = events.Where(e => e.StudentId != null).Select(e => e.StudentId!).Distinct().ToList();
            var names = await db.EnglishProfiles.AsNoTracking().Include(p => p.User)
                .Where(p => studentIds.Contains(p.UserId)).ToDictionaryAsync(p => p.UserId, p => p.DisplayName, ct);
            items.AddRange(events.Select(e => ToItem(e, e.StudentId is not null ? names.GetValueOrDefault(e.StudentId, "ученик")
                : e.Group is not null ? "Группа: " + e.Group.Name : "Все ученики", true)));

            var due = await db.EnglishHomework.AsNoTracking().Include(h => h.Group)
                .Where(h => h.TeacherId == userId && h.DueAt >= fromUtc && h.DueAt < toUtc).ToListAsync(ct);
            items.AddRange(due.Select(h => Deadline(h, h.Group is not null ? "Группа: " + h.Group.Name : "Все ученики")));
        }
        else
        {
            var teacherId = await EnglishProfileService.TeacherOfAsync(db, userId, ct);
            var groups = await EnglishProfileService.GroupsOfAsync(db, userId, ct);
            var events = await db.EnglishEvents.AsNoTracking()
                .Where(e => e.StartsAt >= fromUtc && e.StartsAt < toUtc && (e.OwnerId == userId
                    || (e.OwnerId == teacherId && (e.StudentId == userId
                        || (e.GroupId != null && groups.Contains(e.GroupId.Value))
                        || (e.StudentId == null && e.GroupId == null)))))
                .ToListAsync(ct);
            items.AddRange(events.Select(e => ToItem(e, e.OwnerId == userId ? "Личное" : "От преподавателя", e.OwnerId == userId)));

            if (teacherId is not null)
            {
                var due = await db.EnglishHomework.AsNoTracking()
                    .Where(h => h.TeacherId == teacherId && h.DueAt >= fromUtc && h.DueAt < toUtc
                        && (h.GroupId == null || groups.Contains(h.GroupId.Value)))
                    .ToListAsync(ct);
                items.AddRange(due.Select(h => Deadline(h, "Домашнее задание")));
            }
        }
        return items.OrderBy(i => i.StartsAt).ToList();
    }

    /// <summary>Ближайшие события начиная с текущего момента.</summary>
    public async Task<List<EnglishCalendarItem>> UpcomingAsync(string userId, bool isTeacher, int take, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var list = await ListAsync(userId, isTeacher, now.AddHours(-1), now.AddDays(60), ct);
        return list.Where(i => i.EventId is not null).Take(take).ToList();
    }

    /// <summary>
    /// Создаёт или обновляет событие. Преподаватель может адресовать его ученику или группе; у ученика событие всегда личное.
    /// startLocal — в часовом поясе пользователя.
    /// </summary>
    public async Task<EnglishResult> SaveAsync(string userId, bool isTeacher, EnglishEvent input, DateTime startLocal, TimeZoneInfo tz, CancellationToken ct = default)
    {
        var title = EnglishRoles.Clip(input.Title, 200);
        if (title.Length == 0) return new("Укажите название события.");
        var link = EnglishRoles.Clip(input.Link, 1000);
        if (link.Length > 0 && !EnglishLessonService.IsHttpUrl(link)) return new("Ссылка должна начинаться с http:// или https://.");

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        string? studentId = null;
        Guid? groupId = null;
        if (isTeacher)
        {
            if (input.StudentId is { Length: > 0 } sid)
            {
                if (!await EnglishProfileService.IsStudentOfAsync(db, userId, sid, ct)) return new("Ученик не найден.");
                studentId = sid;
            }
            else if (input.GroupId is { } gid)
            {
                if (!await db.EnglishGroups.AnyAsync(g => g.Id == gid && g.TeacherId == userId, ct)) return new("Группа не найдена.");
                groupId = gid;
            }
        }

        EnglishEvent? e;
        if (input.Id == Guid.Empty)
        {
            e = new EnglishEvent { Id = Guid.NewGuid(), OwnerId = userId, CreatedAt = DateTime.UtcNow };
            db.EnglishEvents.Add(e);
        }
        else
        {
            e = await db.EnglishEvents.FirstOrDefaultAsync(x => x.Id == input.Id && x.OwnerId == userId, ct);
            if (e is null) return new("Событие не найдено.");
        }
        e.Title = title;
        e.Kind = EnglishEvent.Kinds.Contains(input.Kind) ? input.Kind : EnglishEvent.Kinds[0];
        e.StartsAt = EnglishRoles.ToUtc(startLocal, tz);
        e.DurationMinutes = Math.Clamp(input.DurationMinutes, 5, 600);
        e.Link = link;
        e.Notes = EnglishRoles.Clip(input.Notes, 2000);
        e.StudentId = studentId;
        e.GroupId = groupId;
        await db.SaveChangesAsync(ct);
        return EnglishResult.Success;
    }

    public async Task<EnglishEvent?> GetOwnAsync(string userId, Guid id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.EnglishEvents.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id && e.OwnerId == userId, ct);
    }

    public async Task<bool> DeleteAsync(string userId, Guid id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.EnglishEvents.Where(e => e.Id == id && e.OwnerId == userId).ExecuteDeleteAsync(ct) > 0;
    }

    private static EnglishCalendarItem ToItem(EnglishEvent e, string audience, bool canEdit) =>
        new(e.Id, e.Title, e.Kind, e.StartsAt, e.DurationMinutes, e.Link, e.Notes, audience, canEdit);

    private static EnglishCalendarItem Deadline(EnglishHomework h, string audience) =>
        new(null, "Сдать: " + h.Title, "Дедлайн", h.DueAt!.Value, 0, "", "", audience, false);
}
