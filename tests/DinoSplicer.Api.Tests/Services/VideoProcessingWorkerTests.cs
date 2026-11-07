using DinoSplicer.Api.Data;
using DinoSplicer.Api.Models;
using DinoSplicer.Api.Services;

using FluentAssertions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DinoSplicer.Api.Tests.Services;

public class VideoProcessingWorkerTests(VideoApiFactory factory) : IClassFixture<VideoApiFactory>
{
    private async Task<Video> SeedVideoAsync(VideoStatus status, DateTime? createdAt = null)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        Video video = new()
        {
            OriginalFileName = "test.mp4",
            FileName = $"{Guid.NewGuid()}.mp4",
            ContentType = "video/mp4",
            SizeBytes = 1024,
            Status = status,
            CreatedAt = createdAt ?? DateTime.UtcNow,
        };

        db.Videos.Add(video);
        await db.SaveChangesAsync();
        return video;
    }

    private async Task<VideoStatus> GetStatusAsync(Guid id)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Video video = await db.Videos.FindAsync([id]) ?? throw new InvalidOperationException("Video not found");
        return video.Status;
    }

    private VideoProcessingWorker CreateWorker(IBackgroundJobQueue<Video> queue, IVideoMetadataExtractor? extractor = null)
    {
        IServiceScopeFactory scopeFactory = factory.Services.GetRequiredService<IServiceScopeFactory>();
        if (extractor is not null)
        {
            scopeFactory = new ExtractorOverrideScopeFactory(scopeFactory, extractor);
        }

        ILogger<VideoProcessingWorker> logger = NullLogger<VideoProcessingWorker>.Instance;
        return new VideoProcessingWorker(queue, scopeFactory, logger);
    }

    private static async Task<List<Guid>> DrainQueueUntilAsync(IBackgroundJobQueue<Video> queue, Guid expectedId)
    {
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(5));
        List<Guid> dequeuedIds = [];
        await foreach (Guid id in queue.DequeueAllAsync(cts.Token))
        {
            dequeuedIds.Add(id);
            if (id == expectedId)
            {
                break;
            }
        }

        return dequeuedIds;
    }

    private static async Task<List<Guid>> DequeueAsync(IBackgroundJobQueue<Video> queue, int count)
    {
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(5));
        List<Guid> ids = [];
        await foreach (Guid id in queue.DequeueAllAsync(cts.Token))
        {
            ids.Add(id);
            if (ids.Count == count)
            {
                break;
            }
        }

        return ids;
    }

    [Theory]
    [InlineData(VideoStatus.Uploaded)]
    [InlineData(VideoStatus.Processing)]
    public async Task RecoverPendingVideosAsync_UnfinishedVideo_RequeuesVideoId(VideoStatus status)
    {
        Video video = await SeedVideoAsync(status);
        BackgroundJobQueue<Video> queue = new();
        VideoProcessingWorker worker = CreateWorker(queue);

        await worker.RecoverPendingVideosAsync(CancellationToken.None);

        List<Guid> dequeuedIds = await DrainQueueUntilAsync(queue, video.Id);
        dequeuedIds.Should().Contain(video.Id);
    }

    [Theory]
    [InlineData(VideoStatus.Ready)]
    [InlineData(VideoStatus.Failed)]
    public async Task RecoverPendingVideosAsync_FinishedVideo_IsNotRequeued(VideoStatus status)
    {
        Video video = await SeedVideoAsync(status);
        BackgroundJobQueue<Video> queue = new();
        VideoProcessingWorker worker = CreateWorker(queue);

        await worker.RecoverPendingVideosAsync(CancellationToken.None);

        using CancellationTokenSource cts = new(TimeSpan.FromMilliseconds(500));
        List<Guid> dequeuedIds = [];
        Func<Task> drain = async () =>
        {
            await foreach (Guid id in queue.DequeueAllAsync(cts.Token))
            {
                dequeuedIds.Add(id);
            }
        };

        await drain.Should().ThrowAsync<OperationCanceledException>();
        dequeuedIds.Should().NotContain(video.Id);
    }

    [Fact]
    public async Task RecoverPendingAsync_MixedStatuses_EnqueuesOnlyMatchingPendingVideosInOrder()
    {
        DateTime baseTime = DateTime.UtcNow;
        Video olderPending = await SeedVideoAsync(VideoStatus.Processing, baseTime.AddMinutes(-2));
        Video newerPending = await SeedVideoAsync(VideoStatus.Uploaded, baseTime.AddMinutes(-1));
        Video readyVideo = await SeedVideoAsync(VideoStatus.Ready, baseTime);
        Video failedVideo = await SeedVideoAsync(VideoStatus.Failed, baseTime);

        BackgroundJobQueue<Video> queue = new();
        IServiceScopeFactory scopeFactory = factory.Services.GetRequiredService<IServiceScopeFactory>();

        int count = await queue.RecoverPendingAsync(
            scopeFactory,
            NullLogger.Instance,
            jobDescription: "video(s)",
            isPending: v => v.Status == VideoStatus.Uploaded || v.Status == VideoStatus.Processing,
            orderBy: v => v.CreatedAt,
            idSelector: v => v.Id,
            cancellationToken: CancellationToken.None
        );

        count.Should().BeGreaterThanOrEqualTo(2);
        List<Guid> enqueuedIds = await DequeueAsync(queue, count);
        enqueuedIds.Should().ContainInOrder(olderPending.Id, newerPending.Id);
        enqueuedIds.Should().NotContain([readyVideo.Id, failedVideo.Id]);
    }

    [Fact]
    public async Task RecoverPendingAsync_CancellationRequested_ReturnsZeroWithoutEnqueuing()
    {
        await SeedVideoAsync(VideoStatus.Uploaded);
        BackgroundJobQueue<Video> queue = new();
        IServiceScopeFactory scopeFactory = factory.Services.GetRequiredService<IServiceScopeFactory>();
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        int count = await queue.RecoverPendingAsync(
            scopeFactory,
            NullLogger.Instance,
            jobDescription: "video(s)",
            isPending: v => v.Status == VideoStatus.Uploaded || v.Status == VideoStatus.Processing,
            orderBy: v => v.CreatedAt,
            idSelector: v => v.Id,
            cancellationToken: cts.Token
        );

        count.Should().Be(0);
        using CancellationTokenSource drainCts = new(TimeSpan.FromMilliseconds(100));
        Func<Task> drain = async () =>
        {
            await foreach (Guid _ in queue.DequeueAllAsync(drainCts.Token))
            {
            }
        };

        await drain.Should().ThrowAsync<OperationCanceledException>();
    }

    [Theory]
    [InlineData(VideoStatus.Ready)]
    [InlineData(VideoStatus.Failed)]
    public async Task ProcessAsync_AlreadyFinishedVideo_IsSkipped(VideoStatus status)
    {
        Video video = await SeedVideoAsync(status);
        VideoProcessingWorker worker = CreateWorker(new BackgroundJobQueue<Video>(), new HangingExtractor(new TaskCompletionSource()));

        await worker.ProcessAsync(video.Id, CancellationToken.None);

        (await GetStatusAsync(video.Id)).Should().Be(status);
    }

    [Fact]
    public async Task ProcessAsync_CancellationRequested_RevertsToUploadedAndRethrows()
    {
        TaskCompletionSource extractorStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Video video = await SeedVideoAsync(VideoStatus.Uploaded);
        VideoProcessingWorker worker = CreateWorker(new BackgroundJobQueue<Video>(), new HangingExtractor(extractorStarted));

        using CancellationTokenSource cts = new();
        Task processTask = worker.ProcessAsync(video.Id, cts.Token);

        await extractorStarted.Task;
        await cts.CancelAsync();

        await FluentActions.Awaiting(() => processTask).Should().ThrowAsync<OperationCanceledException>();
        (await GetStatusAsync(video.Id)).Should().Be(VideoStatus.Uploaded);
    }

    private sealed class HangingExtractor(TaskCompletionSource started) : IVideoMetadataExtractor
    {
        public async Task<VideoMetadata> ExtractMetadata(
            string filePath,
            bool extractCodec = true,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default
        )
        {
            started.TrySetResult();
            await Task.Delay(Timeout.Infinite, cancellationToken);
            throw new InvalidOperationException("Unreachable");
        }
    }

    private sealed class ExtractorOverrideScopeFactory(IServiceScopeFactory inner, IVideoMetadataExtractor extractor) : IServiceScopeFactory
    {
        public IServiceScope CreateScope() => new OverrideScope(inner.CreateScope(), extractor);

        private sealed class OverrideScope(IServiceScope innerScope, IVideoMetadataExtractor extractor) : IServiceScope, IServiceProvider
        {
            public IServiceProvider ServiceProvider => this;

            public object? GetService(Type serviceType) =>
                serviceType == typeof(IVideoMetadataExtractor) ? extractor : innerScope.ServiceProvider.GetService(serviceType);

            public void Dispose() => innerScope.Dispose();
        }
    }
}
