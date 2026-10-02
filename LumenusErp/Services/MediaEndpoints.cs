using LumenusErp.Data;
using Microsoft.EntityFrameworkCore;

namespace LumenusErp.Services;

/// <summary>GET /media/{id}: отдача загруженных картинок с проверкой доступа по видимости страниц.</summary>
public static class MediaEndpoints
{
    public static void MapMediaEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/media/{id:guid}", async (Guid id, HttpContext ctx, IDbContextFactory<ApplicationDbContext> factory, MediaService media) =>
        {
            await using var db = await factory.CreateDbContextAsync();
            var file = await db.MediaFiles.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id);
            if (file is null)
            {
                return Results.NotFound();
            }

            var visibilities = await db.ContentBlocks.AsNoTracking()
                .Where(b => b.MediaFileId == id)
                .Select(b => b.Page!.Visibility)
                .Distinct()
                .ToListAsync();
            var user = ctx.User;
            var isPublic = visibilities.Contains(PageVisibility.Public);
            var allowed = isPublic
                || user.IsInRole("Admin")
                || (user.Identity?.IsAuthenticated == true && visibilities.Contains(PageVisibility.Authenticated));
            var path = media.PathOf(file);
            if (!allowed || !File.Exists(path))
            {
                // 404, а не 401/403: не раскрываем существование закрытых файлов
                return Results.NotFound();
            }

            var headers = ctx.Response.Headers;
            headers.XContentTypeOptions = "nosniff";
            headers.ContentSecurityPolicy = "default-src 'none'; sandbox";
            headers.CacheControl = isPublic ? "public, max-age=31536000, immutable" : "private, max-age=3600";
            return Results.File(path, file.ContentType);
        });
    }
}
