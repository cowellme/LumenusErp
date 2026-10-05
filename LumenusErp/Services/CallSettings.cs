namespace LumenusErp.Services;

/// <summary>Настройки вкладки «Созвоны»: лимит загрузки и рабочий каталог временных файлов.</summary>
public class CallSettings
{
    public const long DefaultMaxBytes = 2L * 1024 * 1024 * 1024;

    /// <summary>Максимум размера загружаемого файла (Calls:MaxBytes, по умолчанию 2 ГБ).</summary>
    public long MaxBytes { get; }

    /// <summary>
    /// Каталог временных файлов (Calls:WorkPath). По умолчанию <c>&lt;Media:Path&gt;/calls-tmp</c>: в Docker это том uploads,
    /// а не слой контейнера, и многогигабайтные файлы не раздувают его. Каждая запись — подкаталог с именем её Id.
    /// Содержимое чистится при старте и после обработки, поэтому не накапливается.
    /// </summary>
    public string WorkRoot { get; }

    public CallSettings(IConfiguration config, MediaService media)
    {
        MaxBytes = config.GetValue<long?>("Calls:MaxBytes") is > 0 and var max ? max : DefaultMaxBytes;
        var path = config["Calls:WorkPath"];
        WorkRoot = Path.GetFullPath(string.IsNullOrWhiteSpace(path) ? Path.Combine(media.Root, "calls-tmp") : path);
        Directory.CreateDirectory(WorkRoot);
    }

    public string DirOf(Guid id) => Path.Combine(WorkRoot, id.ToString("N"));
}
