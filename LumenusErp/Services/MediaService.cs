using LumenusErp.Data;
using Microsoft.EntityFrameworkCore;

namespace LumenusErp.Services;

/// <summary>Ошибка загрузки файла с текстом, который можно показать администратору.</summary>
public class MediaException(string message) : Exception(message);

/// <summary>
/// Хранилище загруженных картинок: файлы на диске в каталоге Media:Path, метаданные в таблице MediaFiles.
/// Тип файла определяется по сигнатуре (magic bytes), а не по имени и заявленному Content-Type; SVG не принимается.
/// </summary>
public class MediaService
{
    public const long DefaultMaxBytes = 10 * 1024 * 1024;

    private readonly IDbContextFactory<ApplicationDbContext> _factory;
    private readonly ILogger<MediaService> _log;

    public string Root { get; }
    public long MaxBytes { get; }

    public MediaService(IConfiguration config, IWebHostEnvironment env, IDbContextFactory<ApplicationDbContext> factory, ILogger<MediaService> log)
    {
        _factory = factory;
        _log = log;
        // В контейнере ContentRoot = /app, то есть по умолчанию /app/uploads; локально — папка рядом с проектом
        var path = config["Media:Path"];
        Root = Path.GetFullPath(string.IsNullOrWhiteSpace(path) ? Path.Combine(env.ContentRootPath, "uploads") : path);
        MaxBytes = config.GetValue<long?>("Media:MaxBytes") is > 0 and var max ? max : DefaultMaxBytes;
        Directory.CreateDirectory(Root);
    }

    /// <summary>Сохраняет картинку на диск и в БД. Бросает <see cref="MediaException"/>, если файл не подходит.</summary>
    public async Task<MediaFile> SaveAsync(Stream input, string originalName, CancellationToken ct = default)
    {
        var header = new byte[16];
        var read = await input.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, ct);
        var kind = Detect(header.AsSpan(0, read))
            ?? throw new MediaException("Файл не похож на изображение. Допустимы JPEG, PNG, WebP и GIF (SVG не поддерживается).");

        var id = Guid.NewGuid();
        var storedName = id.ToString("N") + kind.Extension;
        var path = Path.Combine(Root, storedName);
        long size = read;
        try
        {
            await using (var fs = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            {
                await fs.WriteAsync(header.AsMemory(0, read), ct);
                var buffer = new byte[81920];
                int n;
                while ((n = await input.ReadAsync(buffer, ct)) > 0)
                {
                    size += n;
                    if (size > MaxBytes)
                    {
                        throw new MediaException($"Файл больше {MaxBytes / 1024 / 1024} МБ.");
                    }
                    await fs.WriteAsync(buffer.AsMemory(0, n), ct);
                }
            }

            var file = new MediaFile
            {
                Id = id,
                OriginalName = Sanitize(originalName),
                ContentType = kind.ContentType,
                Size = size,
                StoredName = storedName,
                CreatedAt = DateTime.UtcNow,
            };
            await using var db = await _factory.CreateDbContextAsync(ct);
            db.MediaFiles.Add(file);
            await db.SaveChangesAsync(ct);
            return file;
        }
        catch
        {
            TryDeleteFile(storedName);
            throw;
        }
    }

    public string PathOf(MediaFile file) => Path.Combine(Root, Path.GetFileName(file.StoredName));

    /// <summary>Удаляет файлы, на которые больше не ссылается ни один блок или урок English Studio (строку и файл на диске).</summary>
    public async Task DeleteIfUnreferencedAsync(IEnumerable<Guid> ids)
    {
        var list = ids.Distinct().ToList();
        if (list.Count == 0)
        {
            return;
        }
        await using var db = await _factory.CreateDbContextAsync();
        var used = await db.ContentBlocks.Where(b => b.MediaFileId != null && list.Contains(b.MediaFileId.Value))
            .Select(b => b.MediaFileId!.Value)
            .Concat(db.EnglishLessons.Where(l => l.PhotoMediaId != null && list.Contains(l.PhotoMediaId.Value)).Select(l => l.PhotoMediaId!.Value))
            .Distinct().ToListAsync();
        var files = await db.MediaFiles.Where(m => list.Contains(m.Id) && !used.Contains(m.Id)).ToListAsync();
        await RemoveAsync(db, files);
    }

    /// <summary>Чистит загруженные, но так и не сохранённые в страницу файлы старше часа.</summary>
    public async Task DeleteOrphansAsync()
    {
        var border = DateTime.UtcNow.AddHours(-1);
        await using var db = await _factory.CreateDbContextAsync();
        var files = await db.MediaFiles
            .Where(m => m.CreatedAt < border && !db.ContentBlocks.Any(b => b.MediaFileId == m.Id) && !db.EnglishLessons.Any(l => l.PhotoMediaId == m.Id))
            .ToListAsync();
        await RemoveAsync(db, files);
    }

    private async Task RemoveAsync(ApplicationDbContext db, List<MediaFile> files)
    {
        if (files.Count == 0)
        {
            return;
        }
        db.MediaFiles.RemoveRange(files);
        await db.SaveChangesAsync();
        foreach (var f in files)
        {
            TryDeleteFile(f.StoredName);
        }
    }

    private void TryDeleteFile(string storedName)
    {
        try
        {
            File.Delete(Path.Combine(Root, Path.GetFileName(storedName)));
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Не удалось удалить файл {Name}", storedName);
        }
    }

    // Имя нужно только для отображения: убираем путь и управляющие символы, режем по длине
    private static string Sanitize(string name)
    {
        var clean = new string(Path.GetFileName(name ?? "").Where(c => !char.IsControl(c)).ToArray()).Trim();
        return clean.Length > 255 ? clean[..255] : clean;
    }

    private sealed record ImageKind(string ContentType, string Extension);

    private static ImageKind? Detect(ReadOnlySpan<byte> h)
    {
        if (h.Length >= 3 && h[0] == 0xFF && h[1] == 0xD8 && h[2] == 0xFF)
        {
            return new("image/jpeg", ".jpg");
        }
        if (h.Length >= 8 && h[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
        {
            return new("image/png", ".png");
        }
        if (h.Length >= 6 && (h[..6].SequenceEqual("GIF87a"u8) || h[..6].SequenceEqual("GIF89a"u8)))
        {
            return new("image/gif", ".gif");
        }
        if (h.Length >= 12 && h[..4].SequenceEqual("RIFF"u8) && h.Slice(8, 4).SequenceEqual("WEBP"u8))
        {
            return new("image/webp", ".webp");
        }
        return null;
    }
}
