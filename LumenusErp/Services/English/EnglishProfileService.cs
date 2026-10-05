using LumenusErp.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace LumenusErp.Services.English;

/// <summary>Ученик в списке преподавателя: профиль, e-mail, группы и сводка активности.</summary>
public record EnglishStudentCard(EnglishProfile Profile, string Email, List<string> Groups, int LessonsCompleted, int LessonsTotal, DateTime? LastActivityAt);

/// <summary>Результат операции с текстом ошибки для пользователя (null — успех).</summary>
public record EnglishResult(string? Error = null)
{
    public bool Ok => Error is null;
    public static readonly EnglishResult Success = new();
}

/// <summary>
/// Профили, связь «преподаватель — ученик», группы, права доступа и заявки с лендинга.
/// Каждый метод преподавателя проверяет, что ученик/группа принадлежат ему: чужие = «не найдено».
/// </summary>
public class EnglishProfileService(IDbContextFactory<ApplicationDbContext> dbFactory, UserManager<ApplicationUser> users)
{
    public const int MaxName = 100, MaxText = 300, MaxBio = 2000, MaxNotes = 5000, MaxComment = 2000;

    /// <summary>Профиль пользователя; создаётся пустым при первом обращении.</summary>
    public async Task<EnglishProfile> GetOrCreateAsync(string userId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var p = await db.EnglishProfiles.Include(x => x.User).FirstOrDefaultAsync(x => x.UserId == userId, ct);
        if (p is not null) return p;
        p = new EnglishProfile { UserId = userId, CreatedAt = DateTime.UtcNow };
        db.EnglishProfiles.Add(p);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Параллельный запрос успел создать профиль
            db.ChangeTracker.Clear();
        }
        return await db.EnglishProfiles.Include(x => x.User).FirstAsync(x => x.UserId == userId, ct);
    }

    /// <summary>Сохраняет поля, которые пользователь правит сам на странице профиля.</summary>
    public async Task<EnglishResult> UpdateOwnAsync(string userId, EnglishProfile input, CancellationToken ct = default)
    {
        if (input.WeeklyGoalMinutes is < 10 or > 2000) return new("Недельная цель: от 10 до 2000 минут.");
        if (!TimeZoneInfo.TryFindSystemTimeZoneById(input.TimeZone ?? "", out _)) return new("Неизвестный часовой пояс.");

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var p = await db.EnglishProfiles.FirstOrDefaultAsync(x => x.UserId == userId, ct);
        if (p is null) return new("Профиль не найден.");
        p.FirstName = EnglishRoles.Clip(input.FirstName, MaxName);
        p.LastName = EnglishRoles.Clip(input.LastName, MaxName);
        p.City = EnglishRoles.Clip(input.City, MaxName);
        p.TimeZone = input.TimeZone!.Trim();
        p.Goal = EnglishRoles.Clip(input.Goal, MaxText);
        p.Interests = EnglishRoles.Clip(input.Interests, MaxText);
        p.Bio = EnglishRoles.Clip(input.Bio, MaxBio);
        p.Level = EnglishRoles.Clip(input.Level, 50);
        p.WeeklyGoalMinutes = input.WeeklyGoalMinutes;
        p.Reminders = input.Reminders;
        await db.SaveChangesAsync(ct);
        return EnglishResult.Success;
    }

    public async Task<EnglishResult> SetWeeklyGoalAsync(string userId, int minutes, CancellationToken ct = default)
    {
        if (minutes is < 10 or > 2000) return new("Выбери от 10 до 2000 минут.");
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        await db.EnglishProfiles.Where(x => x.UserId == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.WeeklyGoalMinutes, minutes), ct);
        return EnglishResult.Success;
    }

    /// <summary>Преподаватель ученика (null — не привязан).</summary>
    public static Task<string?> TeacherOfAsync(ApplicationDbContext db, string studentId, CancellationToken ct = default) =>
        db.EnglishProfiles.Where(p => p.UserId == studentId).Select(p => p.TeacherId).FirstOrDefaultAsync(ct);

    /// <summary>Является ли пользователь учеником этого преподавателя.</summary>
    public static Task<bool> IsStudentOfAsync(ApplicationDbContext db, string teacherId, string studentId, CancellationToken ct = default) =>
        db.EnglishProfiles.AnyAsync(p => p.UserId == studentId && p.TeacherId == teacherId, ct);

    /// <summary>Группы, в которых состоит ученик.</summary>
    public static Task<List<Guid>> GroupsOfAsync(ApplicationDbContext db, string studentId, CancellationToken ct = default) =>
        db.EnglishGroupMembers.Where(m => m.StudentId == studentId).Select(m => m.GroupId).ToListAsync(ct);

    public async Task<EnglishProfile?> GetTeacherProfileAsync(string studentId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var teacherId = await TeacherOfAsync(db, studentId, ct);
        if (teacherId is null) return null;
        return await db.EnglishProfiles.AsNoTracking().Include(p => p.User).FirstOrDefaultAsync(p => p.UserId == teacherId, ct);
    }

    // ── Ученики преподавателя ───────────────────────────────────────

    public async Task<List<EnglishStudentCard>> ListStudentsAsync(string teacherId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var profiles = await db.EnglishProfiles.AsNoTracking().Include(p => p.User)
            .Where(p => p.TeacherId == teacherId)
            .OrderBy(p => p.FirstName).ThenBy(p => p.LastName)
            .ToListAsync(ct);
        var ids = profiles.Select(p => p.UserId).ToList();
        var groups = await db.EnglishGroupMembers.AsNoTracking()
            .Where(m => ids.Contains(m.StudentId) && m.Group!.TeacherId == teacherId)
            .Select(m => new { m.StudentId, m.Group!.Name })
            .ToListAsync(ct);
        var completed = await db.EnglishLessonProgress.AsNoTracking()
            .Where(p => ids.Contains(p.StudentId) && p.CompletedAt != null && p.Lesson!.TeacherId == teacherId)
            .GroupBy(p => p.StudentId)
            .Select(g => new { StudentId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.StudentId, x => x.Count, ct);
        var total = await db.EnglishLessons.CountAsync(l => l.TeacherId == teacherId && l.Published, ct);

        return profiles.Select(p => new EnglishStudentCard(
            p,
            p.User?.Email ?? "",
            groups.Where(g => g.StudentId == p.UserId).Select(g => g.Name).ToList(),
            completed.GetValueOrDefault(p.UserId),
            total,
            p.LastActivityAt)).ToList();
    }

    /// <summary>Профиль ученика для преподавателя; null — не его ученик.</summary>
    public async Task<EnglishProfile?> GetStudentAsync(string teacherId, string studentId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.EnglishProfiles.AsNoTracking().Include(p => p.User)
            .FirstOrDefaultAsync(p => p.UserId == studentId && p.TeacherId == teacherId, ct);
    }

    /// <summary>Привязывает зарегистрированного ученика к преподавателю по e-mail.</summary>
    public async Task<EnglishResult> AddStudentByEmailAsync(string teacherId, string? email, CancellationToken ct = default)
    {
        var clean = (email ?? "").Trim();
        if (clean.Length == 0) return new("Укажите e-mail ученика.");
        var user = await users.FindByEmailAsync(clean);
        if (user is null) return new("Пользователь с таким e-mail не найден. Попросите ученика зарегистрироваться в English Studio.");
        if (!await users.IsInRoleAsync(user, EnglishRoles.Student)) return new("Этот пользователь зарегистрирован не как ученик.");

        var profile = await GetOrCreateAsync(user.Id, ct);
        if (profile.TeacherId == teacherId) return new("Ученик уже в вашем списке.");
        if (profile.TeacherId is not null) return new("Ученик уже занимается с другим преподавателем.");

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        await db.EnglishProfiles.Where(p => p.UserId == user.Id && p.TeacherId == null)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.TeacherId, teacherId), ct);
        return EnglishResult.Success;
    }

    /// <summary>Отвязывает ученика: убирает из групп преподавателя, его работы и результаты остаются.</summary>
    public async Task<bool> RemoveStudentAsync(string teacherId, string studentId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (!await IsStudentOfAsync(db, teacherId, studentId, ct)) return false;
        await db.EnglishGroupMembers.Where(m => m.StudentId == studentId && m.Group!.TeacherId == teacherId).ExecuteDeleteAsync(ct);
        await db.EnglishProfiles.Where(p => p.UserId == studentId)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.TeacherId, (string?)null), ct);
        return true;
    }

    /// <summary>Заметки, комментарий, оценка навыков и права доступа ученика — то, что правит преподаватель.</summary>
    public async Task<EnglishResult> UpdateStudentByTeacherAsync(string teacherId, EnglishProfile input, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var p = await db.EnglishProfiles.FirstOrDefaultAsync(x => x.UserId == input.UserId && x.TeacherId == teacherId, ct);
        if (p is null) return new("Ученик не найден.");
        p.TeacherNotes = EnglishRoles.Clip(input.TeacherNotes, MaxNotes);
        p.TeacherComment = EnglishRoles.Clip(input.TeacherComment, MaxComment);
        p.Level = EnglishRoles.Clip(input.Level, 50);
        p.SkillSpeaking = Math.Clamp(input.SkillSpeaking, 0, 100);
        p.SkillListening = Math.Clamp(input.SkillListening, 0, 100);
        p.SkillReading = Math.Clamp(input.SkillReading, 0, 100);
        p.SkillWriting = Math.Clamp(input.SkillWriting, 0, 100);
        p.SkillGrammar = Math.Clamp(input.SkillGrammar, 0, 100);
        p.SkillVocabulary = Math.Clamp(input.SkillVocabulary, 0, 100);
        p.AllowRecordings = input.AllowRecordings;
        p.AllowDownloads = input.AllowDownloads;
        p.AllowRetakes = input.AllowRetakes;
        await db.SaveChangesAsync(ct);
        return EnglishResult.Success;
    }

    // ── Группы ───────────────────────────────────────────────────────

    public async Task<List<EnglishGroup>> ListGroupsAsync(string teacherId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.EnglishGroups.AsNoTracking().Include(g => g.Members)
            .Where(g => g.TeacherId == teacherId).OrderBy(g => g.Name).ToListAsync(ct);
    }

    /// <summary>Создаёт (Id пустой) или обновляет группу преподавателя.</summary>
    public async Task<EnglishResult> SaveGroupAsync(string teacherId, EnglishGroup input, IEnumerable<string> studentIds, CancellationToken ct = default)
    {
        var name = EnglishRoles.Clip(input.Name, 200);
        if (name.Length == 0) return new("Укажите название группы.");

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        EnglishGroup? g;
        if (input.Id == Guid.Empty)
        {
            g = new EnglishGroup { Id = Guid.NewGuid(), TeacherId = teacherId, CreatedAt = DateTime.UtcNow };
            db.EnglishGroups.Add(g);
        }
        else
        {
            g = await db.EnglishGroups.Include(x => x.Members).FirstOrDefaultAsync(x => x.Id == input.Id && x.TeacherId == teacherId, ct);
            if (g is null) return new("Группа не найдена.");
        }
        g.Name = name;
        g.Level = EnglishRoles.Clip(input.Level, 50);
        g.Schedule = EnglishRoles.Clip(input.Schedule, 200);
        g.Description = EnglishRoles.Clip(input.Description, 1000);

        // В группу попадают только ученики этого преподавателя
        var wanted = studentIds.Distinct().ToList();
        var allowed = await db.EnglishProfiles.Where(p => p.TeacherId == teacherId && wanted.Contains(p.UserId)).Select(p => p.UserId).ToListAsync(ct);
        g.Members.RemoveAll(m => !allowed.Contains(m.StudentId));
        foreach (var id in allowed.Where(id => g.Members.All(m => m.StudentId != id)))
        {
            g.Members.Add(new EnglishGroupMember { GroupId = g.Id, StudentId = id });
        }
        await db.SaveChangesAsync(ct);
        return EnglishResult.Success;
    }

    public async Task<bool> DeleteGroupAsync(string teacherId, Guid id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.EnglishGroups.Where(g => g.Id == id && g.TeacherId == teacherId).ExecuteDeleteAsync(ct) > 0;
    }

    // ── Заявки с лендинга ───────────────────────────────────────────

    public async Task<EnglishResult> AddBookingAsync(string? name, string? contact, string? message, CancellationToken ct = default)
    {
        var n = EnglishRoles.Clip(name, 200);
        var c = EnglishRoles.Clip(contact, 200);
        if (n.Length == 0 || c.Length == 0) return new("Укажите имя и контакт для связи.");
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        db.EnglishBookings.Add(new EnglishBooking
        {
            Id = Guid.NewGuid(),
            Name = n,
            Contact = c,
            Message = EnglishRoles.Clip(message, 2000),
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(ct);
        return EnglishResult.Success;
    }

    public async Task<List<EnglishBooking>> ListBookingsAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.EnglishBookings.AsNoTracking().OrderBy(b => b.Handled).ThenByDescending(b => b.CreatedAt).Take(50).ToListAsync(ct);
    }

    public async Task SetBookingHandledAsync(Guid id, bool handled, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        await db.EnglishBookings.Where(b => b.Id == id).ExecuteUpdateAsync(s => s.SetProperty(b => b.Handled, handled), ct);
    }
}
