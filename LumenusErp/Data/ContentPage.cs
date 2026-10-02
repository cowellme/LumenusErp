namespace LumenusErp.Data;

/// <summary>Кто видит страницу /p/{Slug}.</summary>
public enum PageVisibility
{
    /// <summary>Черновик: только администратор.</summary>
    Draft,
    /// <summary>Любой вошедший пользователь.</summary>
    Authenticated,
    /// <summary>Все, включая поисковики (попадает в sitemap и llms.txt).</summary>
    Public,
}

public enum BlockType
{
    Text,
    Bpmn,
    Image,
}

/// <summary>Страница из блоков; собирается администратором в /admin/pages.</summary>
public class ContentPage
{
    public int Id { get; set; }

    /// <summary>Часть URL: /p/{Slug}; латиница, цифры и дефис, уникален.</summary>
    public string Slug { get; set; } = "";
    public string Title { get; set; } = "";

    /// <summary>1–2 предложения: meta description и строка в llms.txt.</summary>
    public string Summary { get; set; } = "";
    public PageVisibility Visibility { get; set; } = PageVisibility.Draft;
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public List<ContentBlock> Blocks { get; set; } = new();
}

public class ContentBlock
{
    public int Id { get; set; }
    public int PageId { get; set; }
    public ContentPage? Page { get; set; }
    public int Order { get; set; }
    public BlockType Type { get; set; }

    /// <summary>Необязательный заголовок блока (h2).</summary>
    public string Heading { get; set; } = "";

    /// <summary>Markdown, для <see cref="BlockType.Text"/>.</summary>
    public string Text { get; set; } = "";

    /// <summary>BPMN 2.0 XML, для <see cref="BlockType.Bpmn"/>.</summary>
    public string BpmnXml { get; set; } = "";

    /// <summary>Картинка, для <see cref="BlockType.Image"/>.</summary>
    public Guid? MediaFileId { get; set; }
    public MediaFile? MediaFile { get; set; }
    public string Caption { get; set; } = "";
    public string AltText { get; set; } = "";
}

/// <summary>Загруженный файл; сам файл лежит на диске под именем <see cref="StoredName"/>.</summary>
public class MediaFile
{
    public Guid Id { get; set; }
    public string OriginalName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public long Size { get; set; }

    /// <summary>Имя на диске: Guid + расширение, никогда не берётся у пользователя.</summary>
    public string StoredName { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}
