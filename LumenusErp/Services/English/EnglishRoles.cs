namespace LumenusErp.Services.English;

/// <summary>Роли Identity модуля English Studio и общие константы.</summary>
public static class EnglishRoles
{
    public const string Teacher = "Teacher";
    public const string Student = "Student";

    /// <summary>Для [Authorize(Roles = ...)]: страницы, общие для обеих ролей.</summary>
    public const string Any = Teacher + "," + Student;

    public static readonly string[] All = [Teacher, Student];

    /// <summary>Часовой пояс по умолчанию, если в профиле пусто или неизвестный идентификатор.</summary>
    public const string DefaultTimeZone = "Europe/Moscow";

    public static TimeZoneInfo FindTimeZone(string? id)
    {
        if (!string.IsNullOrWhiteSpace(id) && TimeZoneInfo.TryFindSystemTimeZoneById(id, out var tz)) return tz;
        return TimeZoneInfo.TryFindSystemTimeZoneById(DefaultTimeZone, out var msk)
            ? msk
            : TimeZoneInfo.CreateCustomTimeZone("MSK", TimeSpan.FromHours(3), "MSK", "MSK");
    }

    /// <summary>UTC → местное время профиля.</summary>
    public static DateTime ToLocal(DateTime utc, TimeZoneInfo tz) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), tz);

    /// <summary>Местное время профиля (из datetime-local) → UTC.</summary>
    public static DateTime ToUtc(DateTime local, TimeZoneInfo tz) =>
        TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), tz);

    /// <summary>Строка обрезается по краям и укорачивается до max символов.</summary>
    public static string Clip(string? value, int max)
    {
        var v = (value ?? "").Trim();
        return v.Length > max ? v[..max] : v;
    }
}
