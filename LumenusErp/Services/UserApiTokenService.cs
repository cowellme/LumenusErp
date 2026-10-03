using System.Security.Cryptography;
using System.Text;
using LumenusErp.Data;
using Microsoft.EntityFrameworkCore;

namespace LumenusErp.Services;

/// <summary>Личные API-токены: выпуск, список, отзыв, проверка. Хранится только SHA-256 хэш.</summary>
public class UserApiTokenService(IDbContextFactory<ApplicationDbContext> dbFactory)
{
    public const string TokenPrefix = "lmn_";
    public const int MaxName = 100;
    private const int ShownPrefixLength = 8;
    private static readonly TimeSpan LastUsedStep = TimeSpan.FromMinutes(1);

    /// <summary>Создаёт токен. Полный токен возвращается только здесь.</summary>
    public async Task<(UserApiToken Entity, string PlainToken)> CreateAsync(string userId, string name, CancellationToken ct = default)
    {
        var plain = TokenPrefix + Base64Url(RandomNumberGenerator.GetBytes(32));
        var entity = new UserApiToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = name.Trim(),
            TokenHash = Hash(plain),
            Prefix = plain[..ShownPrefixLength],
            CreatedAt = DateTime.UtcNow,
        };
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        db.UserApiTokens.Add(entity);
        await db.SaveChangesAsync(ct);
        return (entity, plain);
    }

    public async Task<List<UserApiToken>> ListAsync(string userId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.UserApiTokens.AsNoTracking().Where(t => t.UserId == userId)
            .OrderByDescending(t => t.CreatedAt).ToListAsync(ct);
    }

    /// <summary>Отзывает токен пользователя. false — токена нет, чужой или уже отозван.</summary>
    public async Task<bool> RevokeAsync(string userId, Guid id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var now = DateTime.UtcNow;
        var n = await db.UserApiTokens.Where(t => t.Id == id && t.UserId == userId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), ct);
        return n > 0;
    }

    /// <summary>Владелец неотозванного токена или null. LastUsedAt обновляется не чаще раза в минуту.</summary>
    public async Task<string?> ValidateAsync(string plainToken, CancellationToken ct = default)
    {
        if (!plainToken.StartsWith(TokenPrefix, StringComparison.Ordinal)) return null;
        var hash = Hash(plainToken);
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var t = await db.UserApiTokens.AsNoTracking()
            .Where(x => x.TokenHash == hash && x.RevokedAt == null)
            .Select(x => new { x.Id, x.UserId, x.LastUsedAt })
            .FirstOrDefaultAsync(ct);
        if (t is null) return null;

        var now = DateTime.UtcNow;
        if (t.LastUsedAt is null || now - t.LastUsedAt.Value >= LastUsedStep)
        {
            await db.UserApiTokens.Where(x => x.Id == t.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.LastUsedAt, now), ct);
        }
        return t.UserId;
    }

    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
