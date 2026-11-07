using System.Linq.Expressions;

using DinoSplicer.Api.Data;

using Microsoft.EntityFrameworkCore;

namespace DinoSplicer.Api.Services;

public static class BackgroundJobQueueExtensions
{
    public static async Task<int> RecoverPendingAsync<TJob, TOrderKey>(
        this IBackgroundJobQueue<TJob> queue,
        IServiceScopeFactory scopeFactory,
        ILogger logger,
        string jobDescription,
        Expression<Func<TJob, bool>> isPending,
        Expression<Func<TJob, TOrderKey>> orderBy,
        Expression<Func<TJob, Guid>> idSelector,
        CancellationToken cancellationToken = default
    ) where TJob : class
    {
        ArgumentNullException.ThrowIfNull(queue);
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentException.ThrowIfNullOrWhiteSpace(jobDescription);
        ArgumentNullException.ThrowIfNull(isPending);
        ArgumentNullException.ThrowIfNull(orderBy);
        ArgumentNullException.ThrowIfNull(idSelector);

        try
        {
            using IServiceScope scope = scopeFactory.CreateScope();
            AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            List<Guid> pendingIds = await db.Set<TJob>()
                .Where(isPending)
                .OrderBy(orderBy)
                .Select(idSelector)
                .ToListAsync(cancellationToken);

            int enqueuedCount = await queue.EnqueueManyAsync(pendingIds, static id => id, cancellationToken);

            if (enqueuedCount > 0)
            {
                logger.LogInformation("Re-queued {Count} unfinished {JobDescription} on startup", enqueuedCount, jobDescription);
            }

            return enqueuedCount;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return 0;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to recover pending {JobDescription} on startup", jobDescription);
            return 0;
        }
    }
}
