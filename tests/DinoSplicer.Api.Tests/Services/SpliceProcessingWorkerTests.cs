using DinoSplicer.Api.Data;
using DinoSplicer.Api.Models;
using DinoSplicer.Api.Services;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace DinoSplicer.Api.Tests.Services;

public class SpliceProcessingWorkerTests(VideoApiFactory factory) : IClassFixture<VideoApiFactory>
{
    [Fact]
    public async Task ProcessAsync_ByLengthJob_SetsReadyAndCreatesOneOutputPerSegment()
    {
        SpliceJob job = await SeedJobAsync(SpliceMode.ByLength, 3);

        await CreateWorker().ProcessAsync(job.Id, CancellationToken.None);

        SpliceJob processed = await LoadJobAsync(job.Id);
        processed.Status.Should().Be(SpliceJobStatus.Ready);
        processed.ErrorMessage.Should().BeNull();
        processed.Outputs.OrderBy(o => o.SegmentIndex).Select(o => o.FileName).Should().Equal(
            "holiday_part01.mp4", "holiday_part02.mp4", "holiday_part03.mp4", "holiday_part04.mp4");
        processed.Outputs.Should().AllSatisfy(o =>
            File.Exists(Path.Combine(factory.StorageDirectory, o.StoragePath)).Should().BeTrue());
    }

    [Fact]
    public async Task ProcessAsync_CorruptSourceFile_SetsFailedWithoutThrowing()
    {
        SpliceJob job = await SeedJobAsync(SpliceMode.ByLength, 3, sourceBytes: "not-a-real-video"u8.ToArray());

        await CreateWorker().ProcessAsync(job.Id, CancellationToken.None);

        SpliceJob processed = await LoadJobAsync(job.Id);
        processed.Status.Should().Be(SpliceJobStatus.Failed);
        processed.ErrorMessage.Should().Be("The video could not be spliced.");
        processed.Outputs.Should().BeEmpty();
        Directory.Exists(OutputDirectory(job.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task ProcessAsync_SizeLimitSmallerThanOneKeyframeStretch_StoresReasonForTheUser()
    {
        SpliceJob job = await SeedJobAsync(SpliceMode.BySize, 1000);

        await CreateWorker().ProcessAsync(job.Id, CancellationToken.None);

        SpliceJob processed = await LoadJobAsync(job.Id);
        processed.Status.Should().Be(SpliceJobStatus.Failed);
        processed.ErrorMessage.Should().Contain("Try a larger size");
    }

    [Fact]
    public async Task ProcessAsync_JobInterruptedWhileProcessing_ReplacesPartialOutput()
    {
        SpliceJob job = await SeedJobAsync(SpliceMode.ByLength, 3, SpliceJobStatus.Processing);
        Directory.CreateDirectory(OutputDirectory(job.Id));
        string leftoverFile = Path.Combine(OutputDirectory(job.Id), "segment_009.mp4");
        await File.WriteAllTextAsync(leftoverFile, "partial output");

        await CreateWorker().ProcessAsync(job.Id, CancellationToken.None);

        (await LoadJobAsync(job.Id)).Status.Should().Be(SpliceJobStatus.Ready);
        File.Exists(leftoverFile).Should().BeFalse();
    }

    [Theory]
    [InlineData(SpliceJobStatus.Pending)]
    [InlineData(SpliceJobStatus.Processing)]
    public async Task RecoverPendingJobsAsync_UnfinishedJob_RequeuesJobId(SpliceJobStatus status)
    {
        SpliceJob job = await SeedJobAsync(SpliceMode.ByLength, 3, status);
        BackgroundJobQueue<SpliceJob> queue = new();

        await CreateWorker(queue).RecoverPendingJobsAsync(CancellationToken.None);

        // Earlier tests in this class may have left other unfinished jobs, so read until this one shows up
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(5));
        List<Guid> dequeuedIds = [];
        await foreach (Guid id in queue.DequeueAllAsync(cts.Token))
        {
            dequeuedIds.Add(id);
            if (id == job.Id)
            {
                break;
            }
        }

        dequeuedIds.Should().Contain(job.Id);
    }

    [Fact]
    public async Task ProcessAsync_StubSplicerSucceeds_SetsReadyAndCreatesOutputsWithoutRealFile()
    {
        SpliceJob job = await SeedJobWithoutFileAsync();
        StubVideoSplicer splicer = new((_, outputDirectory, _, _, _, _) => Task.FromResult<IReadOnlyList<SpliceSegment>>(
        [
            new SpliceSegment(1, Path.Combine(outputDirectory, "segment_000.mp4"), 3.0, 2048),
            new SpliceSegment(2, Path.Combine(outputDirectory, "segment_001.mp4"), 2.5, 1536),
        ]));

        await CreateWorker(splicer: splicer).ProcessAsync(job.Id, CancellationToken.None);

        SpliceJob processed = await LoadJobAsync(job.Id);
        processed.Status.Should().Be(SpliceJobStatus.Ready);
        processed.ErrorMessage.Should().BeNull();
        processed.Outputs.OrderBy(o => o.SegmentIndex).Should().SatisfyRespectively(
            first =>
            {
                first.SegmentIndex.Should().Be(1);
                first.FileName.Should().Be("holiday_part01.mp4");
                first.DurationSeconds.Should().Be(3.0);
                first.SizeBytes.Should().Be(2048);
            },
            second =>
            {
                second.SegmentIndex.Should().Be(2);
                second.FileName.Should().Be("holiday_part02.mp4");
                second.DurationSeconds.Should().Be(2.5);
                second.SizeBytes.Should().Be(1536);
            });
    }

    [Fact]
    public async Task ProcessAsync_StubSplicerThrowsSpliceException_SetsFailedWithExceptionMessageAndCleansUpOutputDirectory()
    {
        SpliceJob job = await SeedJobWithoutFileAsync();
        StubVideoSplicer splicer = new((_, outputDirectory, _, _, _, _) =>
        {
            Directory.CreateDirectory(outputDirectory);
            File.WriteAllText(Path.Combine(outputDirectory, "partial.mp4"), "partial");
            throw new SpliceException("Custom splice error message.");
        });

        await CreateWorker(splicer: splicer).ProcessAsync(job.Id, CancellationToken.None);

        SpliceJob processed = await LoadJobAsync(job.Id);
        processed.Status.Should().Be(SpliceJobStatus.Failed);
        processed.ErrorMessage.Should().Be("Custom splice error message.");
        processed.Outputs.Should().BeEmpty();
        Directory.Exists(OutputDirectory(job.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task ProcessAsync_StubSplicerThrowsGenericException_SetsFailedWithGenericMessage()
    {
        SpliceJob job = await SeedJobWithoutFileAsync();
        StubVideoSplicer splicer = new((_, _, _, _, _, _) => throw new InvalidOperationException("ffmpeg crashed"));

        await CreateWorker(splicer: splicer).ProcessAsync(job.Id, CancellationToken.None);

        SpliceJob processed = await LoadJobAsync(job.Id);
        processed.Status.Should().Be(SpliceJobStatus.Failed);
        processed.ErrorMessage.Should().Be("The video could not be spliced.");
        processed.Outputs.Should().BeEmpty();
    }

    [Fact]
    public async Task ProcessAsync_HoldsProcessingLeaseOnSourceVideoWhileSplicing_AndReleasesLeaseWhenFinished()
    {
        SpliceJob job = await SeedJobWithoutFileAsync();
        TaskCompletionSource splicerStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource releaseSplicer = new(TaskCreationOptions.RunContinuationsAsynchronously);
        VideoAccessCoordinator coordinator = new();

        StubVideoSplicer splicer = new(async (_, outputDirectory, _, _, _, ct) =>
        {
            splicerStarted.TrySetResult();
            await releaseSplicer.Task.WaitAsync(ct);
            return [new SpliceSegment(1, Path.Combine(outputDirectory, "segment_000.mp4"), 3.0, 2048)];
        });

        Task processTask = CreateWorker(splicer: splicer, accessCoordinator: coordinator)
            .ProcessAsync(job.Id, CancellationToken.None);

        await splicerStarted.Task;

        coordinator.TryAcquireDelete(job.SourceVideoId).Should().BeFalse();

        releaseSplicer.SetResult();
        await processTask;

        coordinator.TryAcquireDelete(job.SourceVideoId).Should().BeTrue();
        coordinator.CompleteDelete(job.SourceVideoId);
    }

    [Fact]
    public async Task ProcessAsync_SourceVideoIsBeingDeleted_SetsFailedAndSkipsSplicing()
    {
        SpliceJob job = await SeedJobWithoutFileAsync();
        VideoAccessCoordinator coordinator = new();
        coordinator.TryAcquireDelete(job.SourceVideoId).Should().BeTrue();
        bool splicerCalled = false;
        StubVideoSplicer splicer = new((_, _, _, _, _, _) =>
        {
            splicerCalled = true;
            return Task.FromResult<IReadOnlyList<SpliceSegment>>([]);
        });

        await CreateWorker(splicer: splicer, accessCoordinator: coordinator)
            .ProcessAsync(job.Id, CancellationToken.None);

        coordinator.CompleteDelete(job.SourceVideoId);
        splicerCalled.Should().BeFalse();
        SpliceJob processed = await LoadJobAsync(job.Id);
        processed.Status.Should().Be(SpliceJobStatus.Failed);
        processed.ErrorMessage.Should().Be("The source video is being deleted.");
    }

    private SpliceProcessingWorker CreateWorker(
        IBackgroundJobQueue<SpliceJob>? queue = null,
        IVideoSplicer? splicer = null,
        IVideoAccessCoordinator? accessCoordinator = null
    )
    {
        IServiceScopeFactory scopeFactory = factory.Services.GetRequiredService<IServiceScopeFactory>();
        if (splicer is not null)
        {
            scopeFactory = new SplicerOverrideScopeFactory(scopeFactory, splicer);
        }

        return new SpliceProcessingWorker(
            queue ?? new BackgroundJobQueue<SpliceJob>(),
            scopeFactory,
            NullLogger<SpliceProcessingWorker>.Instance,
            accessCoordinator ?? new VideoAccessCoordinator()
        );
    }

    private string OutputDirectory(Guid jobId)
    {
        return SpliceProcessingWorker.GetOutputDirectory(factory.StorageDirectory, jobId);
    }

    // The source is the generated sample video unless other file contents are given
    private async Task<SpliceJob> SeedJobAsync(
        SpliceMode mode,
        double value,
        SpliceJobStatus status = SpliceJobStatus.Pending,
        byte[]? sourceBytes = null
    )
    {
        Video video = new()
        {
            OriginalFileName = "holiday.mp4",
            ContentType = "video/mp4",
            Status = VideoStatus.Ready,
        };
        video.FileName = $"{video.Id}.mp4";

        Directory.CreateDirectory(factory.StorageDirectory);
        string sourcePath = Path.Combine(factory.StorageDirectory, video.FileName);
        if (sourceBytes is null)
        {
            File.Copy(await SampleVideo.GetPathAsync(), sourcePath);
        }
        else
        {
            await File.WriteAllBytesAsync(sourcePath, sourceBytes);
        }

        video.SizeBytes = new FileInfo(sourcePath).Length;

        SpliceJob job = new()
        {
            SourceVideo = video,
            Mode = mode,
            SegmentValue = value,
            Status = status,
        };

        using IServiceScope scope = factory.Services.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.SpliceJobs.Add(job);
        await db.SaveChangesAsync();

        return job;
    }

    private async Task<SpliceJob> LoadJobAsync(Guid jobId)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.SpliceJobs.Include(j => j.Outputs).SingleAsync(j => j.Id == jobId);
    }

    private async Task<SpliceJob> SeedJobWithoutFileAsync(
        SpliceMode mode = SpliceMode.ByLength,
        double value = 3,
        SpliceJobStatus status = SpliceJobStatus.Pending
    )
    {
        Video video = new()
        {
            OriginalFileName = "holiday.mp4",
            ContentType = "video/mp4",
            SizeBytes = 4096,
            Status = VideoStatus.Ready,
        };
        video.FileName = $"{video.Id}.mp4";

        SpliceJob job = new()
        {
            SourceVideo = video,
            Mode = mode,
            SegmentValue = value,
            Status = status,
        };

        using IServiceScope scope = factory.Services.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.SpliceJobs.Add(job);
        await db.SaveChangesAsync();

        return job;
    }

    private sealed class StubVideoSplicer(
        Func<string, string, SpliceMode, double, bool, CancellationToken, Task<IReadOnlyList<SpliceSegment>>> splice
    ) : IVideoSplicer
    {
        public Task<IReadOnlyList<SpliceSegment>> Splice(
            string sourcePath,
            string outputDirectory,
            SpliceMode mode,
            double value,
            bool overwriteExisting = false,
            CancellationToken cancellationToken = default
        ) => splice(sourcePath, outputDirectory, mode, value, overwriteExisting, cancellationToken);
    }

    private sealed class SplicerOverrideScopeFactory(IServiceScopeFactory inner, IVideoSplicer splicer) : IServiceScopeFactory
    {
        public IServiceScope CreateScope() => new OverrideScope(inner.CreateScope(), splicer);

        private sealed class OverrideScope(IServiceScope innerScope, IVideoSplicer splicer) : IServiceScope, IServiceProvider
        {
            public IServiceProvider ServiceProvider => this;

            public object? GetService(Type serviceType) =>
                serviceType == typeof(IVideoSplicer) ? splicer : innerScope.ServiceProvider.GetService(serviceType);

            public void Dispose() => innerScope.Dispose();
        }
    }
}
