using LumenusErp.Data;
using Microsoft.EntityFrameworkCore;

namespace LumenusErp.Services.English;

/// <summary>Собеседник в списке чатов преподавателя: ученик и число непрочитанных от него.</summary>
public record EnglishContact(string UserId, string Name, int Unread, DateTime? LastAt);

/// <summary>Учебный чат «ученик ↔ его преподаватель». Писать можно только своему преподавателю или своему ученику.</summary>
public class EnglishMessageService(IDbContextFactory<ApplicationDbContext> dbFactory)
{
    public const int MaxText = 4000, PageSize = 200;

    /// <summary>Ученики преподавателя с непрочитанными, сначала с недавней перепиской.</summary>
    public async Task<List<EnglishContact>> ContactsAsync(string teacherId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var students = await db.EnglishProfiles.AsNoTracking().Include(p => p.User)
            .Where(p => p.TeacherId == teacherId).ToListAsync(ct);
        var ids = students.Select(s => s.UserId).ToList();
        var stats = await db.EnglishMessages.AsNoTracking()
            .Where(m => (m.ToId == teacherId && ids.Contains(m.FromId)) || (m.FromId == teacherId && ids.Contains(m.ToId)))
            .GroupBy(m => m.FromId == teacherId ? m.ToId : m.FromId)
            .Select(g => new
            {
                StudentId = g.Key,
                Unread = g.Count(m => m.ToId == teacherId && m.ReadAt == null),
                LastAt = g.Max(m => m.CreatedAt),
            })
            .ToDictionaryAsync(x => x.StudentId, ct);
        return students
            .Select(s => new EnglishContact(s.UserId, s.DisplayName, stats.GetValueOrDefault(s.UserId)?.Unread ?? 0, stats.GetValueOrDefault(s.UserId)?.LastAt))
            .OrderByDescending(c => c.LastAt ?? DateTime.MinValue).ThenBy(c => c.Name)
            .ToList();
    }

    /// <summary>Переписка двух пользователей (последние PageSize), входящие помечаются прочитанными.</summary>
    public async Task<List<EnglishMessage>> ThreadAsync(string userId, string otherId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (!await CanChatAsync(db, userId, otherId, ct)) return new();
        await db.EnglishMessages.Where(m => m.FromId == otherId && m.ToId == userId && m.ReadAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.ReadAt, DateTime.UtcNow), ct);
        var list = await db.EnglishMessages.AsNoTracking()
            .Where(m => (m.FromId == userId && m.ToId == otherId) || (m.FromId == otherId && m.ToId == userId))
            .OrderByDescending(m => m.CreatedAt).Take(PageSize).ToListAsync(ct);
        list.Reverse();
        return list;
    }

    public async Task<EnglishResult> SendAsync(string fromId, string toId, string? text, CancellationToken ct = default)
    {
        var t = (text ?? "").Trim();
        if (t.Length == 0) return new("Пустое сообщение.");
        if (t.Length > MaxText) return new($"Сообщение длиннее {MaxText} символов.");
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (!await CanChatAsync(db, fromId, toId, ct)) return new("Собеседник не найден.");
        db.EnglishMessages.Add(new EnglishMessage { Id = Guid.NewGuid(), FromId = fromId, ToId = toId, Text = t, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync(ct);
        return EnglishResult.Success;
    }

    public async Task<int> UnreadAsync(string userId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.EnglishMessages.CountAsync(m => m.ToId == userId && m.ReadAt == null, ct);
    }

    private static async Task<bool> CanChatAsync(ApplicationDbContext db, string a, string b, CancellationToken ct) =>
        await EnglishProfileService.IsStudentOfAsync(db, a, b, ct) || await EnglishProfileService.IsStudentOfAsync(db, b, a, ct);
}
