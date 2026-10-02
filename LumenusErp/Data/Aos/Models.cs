// Модели восстановлены по использованию в коде, т.к. оригинальный проект Shared отсутствует.
namespace Shared.Models;

public enum Social
{
    Instagram,
    Telegram
}

public class Blogger
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Url { get; set; } = "";
    public Social? Social { get; set; }
    public int Rate { get; set; }
    public string? Resume { get; set; }
    public DateTime UpdateAt { get; set; }
}

public class Setting
{
    public int Id { get; set; }
    public string SystemPrompt { get; set; } = "";
    public int UpdateTime { get; set; }
}

public class TUser
{
    public int Id { get; set; }
    public long TelegramId { get; set; }
    public string? Username { get; set; }
    public DateTime CreatedAt { get; set; }
}
