using System.Threading.Channels;

namespace DinoSplicer.Api.Services;

public class BackgroundJobQueue<TJob> : IBackgroundJobQueue<TJob> where TJob : class
{
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>(new UnboundedChannelOptions
    {
        SingleReader = true,
    });

    public ValueTask EnqueueAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        return _channel.Writer.WriteAsync(jobId, cancellationToken);
    }

    public async ValueTask<int> EnqueueManyAsync<TSource>(
        IEnumerable<TSource> items,
        Func<TSource, Guid> idSelector,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(idSelector);
        cancellationToken.ThrowIfCancellationRequested();

        int enqueuedCount = 0;
        foreach (TSource item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await EnqueueAsync(idSelector(item), cancellationToken);
            enqueuedCount++;
        }

        return enqueuedCount;
    }

    public IAsyncEnumerable<Guid> DequeueAllAsync(CancellationToken cancellationToken)
    {
        return _channel.Reader.ReadAllAsync(cancellationToken);
    }
}
