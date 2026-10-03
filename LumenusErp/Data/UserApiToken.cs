namespace LumenusErp.Data;

/// <summary>Личный API-токен пользователя (для myasi). В БД только SHA-256 хэш; сам токен показывается один раз при создании.</summary>
public class UserApiToken
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = "";
    public ApplicationUser? User { get; set; }
    public string Name { get; set; } = "";

    /// <summary>SHA-256 токена, hex в нижнем регистре.</summary>
    public string TokenHash { get; set; } = "";

    /// <summary>Первые символы токена — чтобы пользователь узнавал его в списке.</summary>
    public string Prefix { get; set; } = "";

    public DateTime CreatedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public DateTime? RevokedAt { get; set; }
}
