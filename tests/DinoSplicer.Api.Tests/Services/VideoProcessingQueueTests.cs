using DinoSplicer.Api.Services;

using FluentAssertions;

namespace DinoSplicer.Api.Tests.Services;

public class VideoProcessingQueueTests
{
    [Fact]
    public async Task DequeueAllAsync_AfterEnqueue_YieldsIdsInFifoOrder()
    {
        VideoProcessingQueue queue = new();
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
        VideoProcessingQueue queue = new();
        using CancellationTokenSource cts = new(TimeSpan.FromMilliseconds(50));

        Func<Task> act = async () =>
        {
            await foreach (Guid _ in queue.DequeueAllAsync(cts.Token))
            {
            }
        };

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
