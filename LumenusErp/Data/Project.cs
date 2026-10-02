namespace LumenusErp.Data;

/// <summary>Значения <see cref="Project.Status"/>.</summary>
public static class ProjectStatus
{
    public const string Done = "Завершено";
    public const string InProgress = "В работе";

    public static readonly string[] All = [Done, InProgress];
}

/// <summary>Проект из публичного реестра; тексты правит администратор в /admin/projects.</summary>
public class Project
{
    public int Id { get; set; }

    /// <summary>Часть URL: /projects/{Slug}; латиница, цифры и дефис, уникален.</summary>
    public string Slug { get; set; } = "";
    public string Client { get; set; } = "";
    public string Title { get; set; } = "";
    public string Direction { get; set; } = "";
    public int Year { get; set; }
    public string Status { get; set; } = ProjectStatus.InProgress;
    public List<string> Tags { get; set; } = new();

    /// <summary>1–2 предложения: строка реестра и meta description.</summary>
    public string Summary { get; set; } = "";

    // Markdown
    public string Challenge { get; set; } = "";
    public string Solution { get; set; } = "";
    public string Result { get; set; } = "";

    public string Duration { get; set; } = "";
    public string Team { get; set; } = "";
    public int SortOrder { get; set; }
    public bool IsPublished { get; set; } = true;
    public DateTime UpdatedAt { get; set; }
}
