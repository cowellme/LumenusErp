using System.Security;
using System.Text;
using LumenusErp.Data;
using Microsoft.EntityFrameworkCore;

namespace LumenusErp.Services;

/// <summary>Динамические /sitemap.xml, /llms.txt, /llms-full.txt: статический шаблон + данные из БД.</summary>
public static class SeoEndpoints
{
    public static void MapSeoEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/sitemap.xml", async (IDbContextFactory<ApplicationDbContext> factory) =>
        {
            await using var db = await factory.CreateDbContextAsync();
            var projects = await db.Projects.AsNoTracking()
                .Where(p => p.IsPublished)
                .OrderBy(p => p.SortOrder)
                .Select(p => new { p.Slug, p.UpdatedAt })
                .ToListAsync();

            var sb = new StringBuilder();
            sb.AppendLine("""<?xml version="1.0" encoding="UTF-8"?>""");
            sb.AppendLine("""<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">""");
            AppendUrl(sb, "/", null);
            AppendUrl(sb, "/projects", projects.Count > 0 ? projects.Max(p => p.UpdatedAt) : null);
            AppendUrl(sb, "/faq", null);
            foreach (var p in projects)
            {
                AppendUrl(sb, "/projects/" + p.Slug, p.UpdatedAt);
            }
            sb.AppendLine("</urlset>");
            return Results.Text(sb.ToString(), "application/xml; charset=utf-8");
        });

        app.MapGet("/llms.txt", async (IDbContextFactory<ApplicationDbContext> factory, IWebHostEnvironment env) =>
        {
            var sb = new StringBuilder(await ReadTemplateAsync(env, "llms.txt"));
            var projects = await LoadProjectsAsync(factory);
            sb.AppendLine();
            sb.AppendLine("## Projects");
            sb.AppendLine();
            foreach (var p in projects)
            {
                sb.AppendLine($"- [{p.Title}]({SiteInfo.Url("/projects/" + p.Slug)}): {p.Summary}");
            }
            return Results.Text(sb.ToString(), "text/plain; charset=utf-8");
        });

        app.MapGet("/llms-full.txt", async (IDbContextFactory<ApplicationDbContext> factory, IWebHostEnvironment env) =>
        {
            var sb = new StringBuilder(await ReadTemplateAsync(env, "llms-full.txt"));
            var projects = await LoadProjectsAsync(factory);
            sb.AppendLine();
            sb.AppendLine($"## Проекты ({SiteInfo.Url("/projects")})");
            foreach (var p in projects)
            {
                sb.AppendLine();
                sb.AppendLine($"### {p.Title}");
                sb.AppendLine();
                sb.AppendLine($"Заказчик: {p.Client}. Направление: {p.Direction}. Год: {p.Year}. Статус: {p.Status}. Срок: {p.Duration}. Команда: {p.Team}.");
                sb.AppendLine();
                sb.AppendLine($"Страница: {SiteInfo.Url("/projects/" + p.Slug)}");
                sb.AppendLine();
                sb.AppendLine(p.Summary);
                if (p.Tags.Count > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("Технологии: " + string.Join(", ", p.Tags) + ".");
                }
                AppendSection(sb, "Задача", p.Challenge);
                AppendSection(sb, "Решение", p.Solution);
                AppendSection(sb, "Результат", p.Result);
            }
            return Results.Text(sb.ToString(), "text/plain; charset=utf-8");
        });
    }

    private static void AppendUrl(StringBuilder sb, string path, DateTime? lastMod)
    {
        sb.AppendLine("  <url>");
        sb.AppendLine($"    <loc>{SecurityElement.Escape(SiteInfo.Url(path))}</loc>");
        if (lastMod is { } d)
        {
            sb.AppendLine($"    <lastmod>{d:yyyy-MM-dd}</lastmod>");
        }
        sb.AppendLine("  </url>");
    }

    // Подзаголовки уровня ####, чтобы markdown внутри секций не ломал иерархию документа.
    private static void AppendSection(StringBuilder sb, string title, string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return;
        }
        sb.AppendLine();
        sb.AppendLine($"#### {title}");
        sb.AppendLine();
        sb.AppendLine(markdown.Trim());
    }

    private static async Task<List<Project>> LoadProjectsAsync(IDbContextFactory<ApplicationDbContext> factory)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Projects.AsNoTracking()
            .Where(p => p.IsPublished)
            .OrderBy(p => p.SortOrder)
            .ToListAsync();
    }

    private static Task<string> ReadTemplateAsync(IWebHostEnvironment env, string name) =>
        File.ReadAllTextAsync(Path.Combine(env.ContentRootPath, "Content", name));
}
