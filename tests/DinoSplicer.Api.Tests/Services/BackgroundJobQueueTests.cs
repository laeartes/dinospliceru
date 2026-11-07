using DinoSplicer.Api.Models;
using DinoSplicer.Api.Services;

using FluentAssertions;

using Microsoft.Extensions.DependencyInjection;

namespace DinoSplicer.Api.Tests.Services;

public class BackgroundJobQueueTests
{
    [Fact]
    public async Task DequeueAllAsync_AfterEnqueue_YieldsIdsInFifoOrder()
    {
        BackgroundJobQueue<Video> queue = new();
        Guid first = Guid.NewGuid();
        Guid second = Guid.NewGuid();
        await queue.EnqueueAsync(first);
        await queue.EnqueueAsync(second);

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(5));
        List<Guid> received = [];
        await foreach (Guid id in queue.DequeueAllAsync(cts.Token))
        {
            received.Add(id);
            if (received.Count == 2)
            {
                break;
            }
        }

        received.Should().Equal(first, second);
    }

    [Fact]
    public async Task DequeueAllAsync_EmptyQueueAndCancelled_ThrowsOperationCanceledException()
    {
        BackgroundJobQueue<Video> queue = new();
        using CancellationTokenSource cts = new(TimeSpan.FromMilliseconds(50));

        Func<Task> act = async () =>
        {
            await foreach (Guid _ in queue.DequeueAllAsync(cts.Token))
            {
            }
        };

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ServiceProvider_DifferentJobTypes_ResolvesIndependentSingletonsThatDoNotShareItems()
    {
        ServiceCollection services = new();
        services.AddSingleton(typeof(IBackgroundJobQueue<>), typeof(BackgroundJobQueue<>));
        await using ServiceProvider provider = services.BuildServiceProvider();

        IBackgroundJobQueue<Video> videoQueueFirst = provider.GetRequiredService<IBackgroundJobQueue<Video>>();
        IBackgroundJobQueue<Video> videoQueueSecond = provider.GetRequiredService<IBackgroundJobQueue<Video>>();
        IBackgroundJobQueue<SpliceJob> spliceQueue = provider.GetRequiredService<IBackgroundJobQueue<SpliceJob>>();

        videoQueueFirst.Should().BeSameAs(videoQueueSecond);

        Guid videoId = Guid.NewGuid();
        Guid spliceJobId = Guid.NewGuid();
        await videoQueueFirst.EnqueueAsync(videoId);
        await spliceQueue.EnqueueAsync(spliceJobId);

        (await DequeueSingleAsync(videoQueueSecond)).Should().Be(videoId);
        (await DequeueSingleAsync(spliceQueue)).Should().Be(spliceJobId);
        await AssertQueueIsEmptyAsync(videoQueueFirst);
        await AssertQueueIsEmptyAsync(spliceQueue);
    }

    [Fact]
    public async Task EnqueueManyAsync_EntitySequence_EnqueuesSelectedIdsInFifoOrderAndReturnsCount()
    {
        BackgroundJobQueue<Video> queue = new();
        Video first = new() { OriginalFileName = "first.mp4" };
        Video second = new() { OriginalFileName = "second.mp4" };

        int enqueuedCount = await queue.EnqueueManyAsync([first, second], static v => v.Id);

        enqueuedCount.Should().Be(2);
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(5));
        List<Guid> received = [];
        await foreach (Guid id in queue.DequeueAllAsync(cts.Token))
        {
            received.Add(id);
            if (received.Count == 2)
            {
                break;
            }
        }

        received.Should().Equal(first.Id, second.Id);
    }

    [Fact]
    public async Task EnqueueManyAsync_CancelledMidSequence_EnqueuesOnlyItemsBeforeCancellationAndThrows()
    {
        BackgroundJobQueue<Video> queue = new();
        Guid firstId = Guid.NewGuid();
        Guid secondId = Guid.NewGuid();
        using CancellationTokenSource cts = new();

        IEnumerable<Guid> ItemsYieldingCancellation()
        {
            yield return firstId;
            cts.Cancel();
            yield return secondId;
        }

        Func<Task> act = async () => await queue.EnqueueManyAsync(ItemsYieldingCancellation(), static id => id, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        (await DequeueSingleAsync(queue)).Should().Be(firstId);
        await AssertQueueIsEmptyAsync(queue);
    }

    [Fact]
    public async Task EnqueueManyAsync_AlreadyCancelledTokenWithEmptyInput_ThrowsOperationCanceledException()
    {
        BackgroundJobQueue<Video> queue = new();
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        Func<Task> act = async () => await queue.EnqueueManyAsync(Array.Empty<Video>(), static v => v.Id, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    private static async Task<Guid> DequeueSingleAsync<TJob>(IBackgroundJobQueue<TJob> queue) where TJob : class
    {
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(5));
        await foreach (Guid id in queue.DequeueAllAsync(cts.Token))
        {
            return id;
        }

        throw new InvalidOperationException("Queue closed without yielding an item.");
    }

    private static async Task AssertQueueIsEmptyAsync<TJob>(IBackgroundJobQueue<TJob> queue) where TJob : class
    {
        using CancellationTokenSource cts = new(TimeSpan.FromMilliseconds(50));
        Func<Task> drain = async () =>
        {
            await foreach (Guid _ in queue.DequeueAllAsync(cts.Token))
            {
            }
        };

        await drain.Should().ThrowAsync<OperationCanceledException>();
    }
}
