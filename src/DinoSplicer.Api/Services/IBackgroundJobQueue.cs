namespace DinoSplicer.Api.Services;

public interface IBackgroundJobQueue<TJob> where TJob : class
{
    ValueTask EnqueueAsync(Guid jobId, CancellationToken cancellationToken = default);

    ValueTask<int> EnqueueManyAsync<TSource>(
        IEnumerable<TSource> items,
        Func<TSource, Guid> idSelector,
        CancellationToken cancellationToken = default
    );

    IAsyncEnumerable<Guid> DequeueAllAsync(CancellationToken cancellationToken);
}
