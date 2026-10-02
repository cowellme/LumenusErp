namespace LumenusErp;

/// <summary>Публичные константы сайта: единственное место, где задан базовый URL.</summary>
public static class SiteInfo
{
    public const string BaseUrl = "https://lumenustech.ru";
    public const string Name = "LumenusTech";
    public const string Email = "info@lumenustech.ru";
    public const string TelegramUrl = "https://t.me/lumenustech_bot";

    /// <summary>Абсолютный URL по пути сайта ("/" или "/projects").</summary>
    public static string Url(string path) => BaseUrl + path;
}
