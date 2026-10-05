using System.Globalization;

namespace LumenusErp.Services.English;

/// <summary>Форматирование для страниц English Studio: инициалы, склонения, даты по-русски.</summary>
public static class EnglishText
{
    public static readonly CultureInfo Ru = CultureInfo.GetCultureInfo("ru-RU");
    public static readonly string[] WeekDays = ["Пн", "Вт", "Ср", "Чт", "Пт", "Сб", "Вс"];

    /// <summary>Две буквы для аватара: «Анна Соколова» → «АС», e-mail → первая буква.</summary>
    public static string Initials(string? name)
    {
        var parts = (name ?? "").Split([' ', '@', '.'], StringSplitOptions.RemoveEmptyEntries);
        return parts.Length switch
        {
            0 => "?",
            1 => parts[0][..1].ToUpper(Ru),
            _ => (parts[0][..1] + parts[1][..1]).ToUpper(Ru),
        };
    }

    /// <summary>Склонение: Plural(5, "урок", "урока", "уроков") → «уроков».</summary>
    public static string Plural(int n, string one, string few, string many)
    {
        var m100 = Math.Abs(n) % 100;
        var m10 = m100 % 10;
        if (m100 is >= 11 and <= 14) return many;
        return m10 switch
        {
            1 => one,
            >= 2 and <= 4 => few,
            _ => many,
        };
    }

    /// <summary>«1 ч 25 м» / «40 м».</summary>
    public static string Duration(int minutes) =>
        minutes >= 60 ? $"{minutes / 60} ч {minutes % 60} м" : $"{minutes} м";

    public static string Date(DateTime local) => local.ToString("d MMMM", Ru);
    public static string DateAndTime(DateTime local) => local.ToString("d MMMM, HH:mm", Ru);
    public static string Full(DateTime local) => local.ToString("dd.MM.yyyy HH:mm", Ru);

    /// <summary>«ПОНЕДЕЛЬНИК, 28 СЕНТЯБРЯ».</summary>
    public static string TodayHeader(DateTime local) => local.ToString("dddd, d MMMM", Ru).ToUpper(Ru);

    public static string Month(DateTime local) => Capitalize(local.ToString("MMMM yyyy", Ru));

    public static string Capitalize(string s) => s.Length == 0 ? s : char.ToUpper(s[0], Ru) + s[1..];
}
