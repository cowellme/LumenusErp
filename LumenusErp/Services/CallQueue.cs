using System.Threading.Channels;

namespace LumenusErp.Services;

/// <summary>Очередь записей на обработку: эндпоинт загрузки кладёт Id, <see cref="CallProcessor"/> забирает по одному.</summary>
public class CallQueue
{
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>(new() { SingleReader = true });

    public void Enqueue(Guid id) => _channel.Writer.TryWrite(id);

    public IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken ct) => _channel.Reader.ReadAllAsync(ct);
}
