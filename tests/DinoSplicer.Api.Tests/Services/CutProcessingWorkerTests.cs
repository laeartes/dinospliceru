using DinoSplicer.Api.Data;
using DinoSplicer.Api.Exceptions;
using DinoSplicer.Api.Models;
using DinoSplicer.Api.Services;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace DinoSplicer.Api.Tests.Services;

public class CutProcessingWorkerTests(VideoApiFactory factory) : IClassFixture<VideoApiFactory>
{
    [Fact]
    public async Task ProcessAsync_PendingJob_SetsReadyAndRecordsOutputMetadata()
    {
        CutJob job = await SeedJobWithoutFileAsync();
        StubVideoCutter cutter = new((_, _, outputDirectory, _) =>
            Task.FromResult(new CutResult(Path.Combine(outputDirectory, "clip.mp4"), 3.0, 2048)));

        await CreateWorker(cutter: cutter).ProcessAsync(job.Id, CancellationToken.None);

        CutJob processed = await LoadJobAsync(job.Id);
        processed.Status.Should().Be(CutJobStatus.Ready);
        processed.ErrorMessage.Should().BeNull();
        processed.OutputFileName.Should().Be("holiday_cut.mp4");
        processed.OutputStoragePath.Should().Be(Path.Combine(CutJob.OutputDirectoryName, job.Id.ToString(), "clip.mp4"));
        processed.OutputDurationSeconds.Should().Be(3.0);
        processed.OutputSizeBytes.Should().Be(2048);
    }

    [Fact]
    public async Task ProcessAsync_PendingJob_PassesJobParametersAndPathsToCutter()
    {
        CutJob job = await SeedJobWithoutFileAsync();
        string? receivedSourcePath = null;
        string? receivedOutputDirectory = null;
        StubVideoCutter cutter = new((_, sourcePath, outputDirectory, _) =>
        {
            receivedSourcePath = sourcePath;
            receivedOutputDirectory = outputDirectory;
            return Task.FromResult(new CutResult(Path.Combine(outputDirectory, "clip.mp4"), 3.0, 2048));
        });

        await CreateWorker(cutter: cutter).ProcessAsync(job.Id, CancellationToken.None);

        receivedSourcePath.Should().Be(Path.Combine(factory.StorageDirectory, $"{job.SourceVideoId}.mp4"));
        receivedOutputDirectory.Should().Be(CutJob.GetOutputDirectory(factory.StorageDirectory, job.Id));
    }

    [Fact]
    public async Task ProcessAsync_SuccessfulCut_ClearsProgress()
    {
        CutJob job = await SeedJobWithoutFileAsync();
        JobProgressTracker tracker = new();
        StubVideoCutter cutter = new((jobId, _, outputDirectory, _) =>
        {
            tracker.ReportProgress(jobId, 100);
            return Task.FromResult(new CutResult(Path.Combine(outputDirectory, "clip.mp4"), 3.0, 2048));
        });

        await CreateWorker(cutter: cutter, progressTracker: tracker).ProcessAsync(job.Id, CancellationToken.None);

        tracker.GetProgress(job.Id).Should().BeNull();
    }

    [Fact]
    public async Task ProcessAsync_CutterThrowsMediaProcessingException_SetsFailedWithMessageCleansUpAndClearsProgress()
    {
        CutJob job = await SeedJobWithoutFileAsync();
        JobProgressTracker tracker = new();
        StubVideoCutter cutter = new((jobId, _, outputDirectory, _) =>
        {
            tracker.ReportProgress(jobId, 40);
            Directory.CreateDirectory(outputDirectory);
            File.WriteAllText(Path.Combine(outputDirectory, "clip.mp4"), "partial");
            throw new MediaProcessingException("Custom cut error message.");
        });

        await CreateWorker(cutter: cutter, progressTracker: tracker).ProcessAsync(job.Id, CancellationToken.None);

        CutJob processed = await LoadJobAsync(job.Id);
        processed.Status.Should().Be(CutJobStatus.Failed);
        processed.ErrorMessage.Should().Be("Custom cut error message.");
        processed.OutputFileName.Should().BeNull();
        processed.OutputStoragePath.Should().BeNull();
        Directory.Exists(OutputDirectory(job.Id)).Should().BeFalse();
        tracker.GetProgress(job.Id).Should().BeNull();
    }

    [Fact]
    public async Task ProcessAsync_CutterThrowsUnexpectedException_SetsFailedWithGenericMessage()
    {
        CutJob job = await SeedJobWithoutFileAsync();
        StubVideoCutter cutter = new((_, _, _, _) => throw new InvalidOperationException("internal details"));

        await CreateWorker(cutter: cutter).ProcessAsync(job.Id, CancellationToken.None);

        CutJob processed = await LoadJobAsync(job.Id);
        processed.Status.Should().Be(CutJobStatus.Failed);
        processed.ErrorMessage.Should().Be(CutProcessingWorker.GenericFailureMessage);
    }

    [Theory]
    [InlineData(CutJobStatus.Ready)]
    [InlineData(CutJobStatus.Failed)]
    public async Task ProcessAsync_JobAlreadyFinished_SkipsCutterAndKeepsStatus(CutJobStatus status)
    {
        CutJob job = await SeedJobWithoutFileAsync(status);
        bool cutterCalled = false;
        StubVideoCutter cutter = new((_, _, outputDirectory, _) =>
        {
            cutterCalled = true;
            return Task.FromResult(new CutResult(Path.Combine(outputDirectory, "clip.mp4"), 3.0, 2048));
        });

        await CreateWorker(cutter: cutter).ProcessAsync(job.Id, CancellationToken.None);

        cutterCalled.Should().BeFalse();
        (await LoadJobAsync(job.Id)).Status.Should().Be(status);
    }

    [Fact]
    public async Task ProcessAsync_JobNoLongerExists_DoesNothing()
    {
        bool cutterCalled = false;
        StubVideoCutter cutter = new((_, _, outputDirectory, _) =>
        {
            cutterCalled = true;
            return Task.FromResult(new CutResult(Path.Combine(outputDirectory, "clip.mp4"), 3.0, 2048));
        });

        Func<Task> act = () => CreateWorker(cutter: cutter).ProcessAsync(Guid.NewGuid(), CancellationToken.None);

        await act.Should().NotThrowAsync();
        cutterCalled.Should().BeFalse();
    }

    [Fact]
    public async Task ProcessAsync_HoldsProcessingLeaseWhileCutting_AndReleasesItWhenFinished()
    {
        CutJob job = await SeedJobWithoutFileAsync();
        TaskCompletionSource cutterStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource releaseCutter = new(TaskCreationOptions.RunContinuationsAsynchronously);
        VideoAccessCoordinator coordinator = new();
        StubVideoCutter cutter = new(async (_, _, outputDirectory, ct) =>
        {
            cutterStarted.TrySetResult();
            await releaseCutter.Task.WaitAsync(ct);
            return new CutResult(Path.Combine(outputDirectory, "clip.mp4"), 3.0, 2048);
        });

        Task processTask = CreateWorker(cutter: cutter, accessCoordinator: coordinator)
            .ProcessAsync(job.Id, CancellationToken.None);
        await cutterStarted.Task;

        // While cutting, the source video cannot be deleted
        coordinator.TryAcquireDelete(job.SourceVideoId).Should().BeFalse();

        releaseCutter.SetResult();
        await processTask;

        coordinator.TryAcquireDelete(job.SourceVideoId).Should().BeTrue();
        coordinator.CompleteDelete(job.SourceVideoId);
    }

    [Fact]
    public async Task ProcessAsync_SourceVideoIsBeingDeleted_SetsFailedAndSkipsCutting()
    {
        CutJob job = await SeedJobWithoutFileAsync();
        VideoAccessCoordinator coordinator = new();
        coordinator.TryAcquireDelete(job.SourceVideoId).Should().BeTrue();
        bool cutterCalled = false;
        StubVideoCutter cutter = new((_, _, outputDirectory, _) =>
        {
            cutterCalled = true;
            return Task.FromResult(new CutResult(Path.Combine(outputDirectory, "clip.mp4"), 3.0, 2048));
        });

        await CreateWorker(cutter: cutter, accessCoordinator: coordinator).ProcessAsync(job.Id, CancellationToken.None);

        coordinator.CompleteDelete(job.SourceVideoId);
        cutterCalled.Should().BeFalse();
        CutJob processed = await LoadJobAsync(job.Id);
        processed.Status.Should().Be(CutJobStatus.Failed);
        processed.ErrorMessage.Should().Be(CutProcessingWorker.SourceBeingDeletedMessage);
    }

    [Fact]
    public async Task ProcessAsync_RealCutterOnSampleVideo_SetsReadyAndWritesClip()
    {
        CutJob job = await SeedJobWithSampleVideoAsync();

        await CreateWorker().ProcessAsync(job.Id, CancellationToken.None);

        CutJob processed = await LoadJobAsync(job.Id);
        processed.Status.Should().Be(CutJobStatus.Ready);
        processed.OutputFileName.Should().Be("holiday_cut.mp4");
        processed.OutputDurationSeconds.Should().BeGreaterThan(0);
        processed.OutputSizeBytes.Should().BeGreaterThan(0);
        File.Exists(Path.Combine(factory.StorageDirectory, processed.OutputStoragePath!)).Should().BeTrue();
    }

    [Theory]
    [InlineData(CutJobStatus.Pending)]
    [InlineData(CutJobStatus.Processing)]
    public async Task RecoverPendingJobsAsync_UnfinishedJob_RequeuesIt(CutJobStatus status)
    {
        CutJob job = await SeedJobWithoutFileAsync(status);
        BackgroundJobQueue<CutJob> queue = new();

        await CreateWorker(queue).RecoverPendingJobsAsync(CancellationToken.None);

        (await ReadQueuedIdsAsync(queue)).Should().Contain(job.Id);
    }

    [Theory]
    [InlineData(CutJobStatus.Ready)]
    [InlineData(CutJobStatus.Failed)]
    public async Task RecoverPendingJobsAsync_FinishedJob_IsNotRequeued(CutJobStatus status)
    {
        CutJob job = await SeedJobWithoutFileAsync(status);
        BackgroundJobQueue<CutJob> queue = new();

        await CreateWorker(queue).RecoverPendingJobsAsync(CancellationToken.None);

        (await ReadQueuedIdsAsync(queue)).Should().NotContain(job.Id);
    }

    private CutProcessingWorker CreateWorker(
        IBackgroundJobQueue<CutJob>? queue = null,
        IVideoCutter? cutter = null,
        IVideoAccessCoordinator? accessCoordinator = null,
        IJobProgressTracker? progressTracker = null
    )
    {
        IServiceScopeFactory scopeFactory = factory.Services.GetRequiredService<IServiceScopeFactory>();
        if (cutter is not null)
        {
            scopeFactory = new CutterOverrideScopeFactory(scopeFactory, cutter);
        }

        return new CutProcessingWorker(
            queue ?? new BackgroundJobQueue<CutJob>(),
            accessCoordinator ?? new VideoAccessCoordinator(),
            progressTracker ?? new JobProgressTracker(),
            scopeFactory,
            NullLogger<CutProcessingWorker>.Instance
        );
    }

    // A fresh queue holds only what recovery added. Jobs left unfinished by earlier tests in this class are
    // included too, so read everything until the queue stays empty for a moment
    private static async Task<List<Guid>> ReadQueuedIdsAsync(BackgroundJobQueue<CutJob> queue)
    {
        List<Guid> ids = [];
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(1));
        try
        {
            await foreach (Guid id in queue.DequeueAllAsync(timeout.Token))
            {
                ids.Add(id);
            }
        }
        catch (OperationCanceledException)
        {
            // Nothing more in the queue
        }

        return ids;
    }

    private string OutputDirectory(Guid jobId)
    {
        return CutJob.GetOutputDirectory(factory.StorageDirectory, jobId);
    }

    private async Task<CutJob> SeedJobWithoutFileAsync(CutJobStatus status = CutJobStatus.Pending)
    {
        Video video = new()
        {
            OriginalFileName = "holiday.mp4",
            ContentType = "video/mp4",
            SizeBytes = 4096,
            DurationSeconds = 10,
            Status = VideoStatus.Ready,
        };
        video.FileName = $"{video.Id}.mp4";

        return await SaveJobAsync(video, status);
    }

    private async Task<CutJob> SeedJobWithSampleVideoAsync()
    {
        Video video = new()
        {
            OriginalFileName = "holiday.mp4",
            ContentType = "video/mp4",
            DurationSeconds = SampleVideo.DurationSeconds,
            Status = VideoStatus.Ready,
        };
        video.FileName = $"{video.Id}.mp4";

        Directory.CreateDirectory(factory.StorageDirectory);
        string sourcePath = Path.Combine(factory.StorageDirectory, video.FileName);
        File.Copy(await SampleVideo.GetPathAsync(), sourcePath);
        video.SizeBytes = new FileInfo(sourcePath).Length;

        return await SaveJobAsync(video, CutJobStatus.Pending);
    }

    private async Task<CutJob> SaveJobAsync(Video video, CutJobStatus status)
    {
        CutJob job = new()
        {
            SourceVideo = video,
            StartSeconds = 1.0,
            EndSeconds = 4.0,
            Mode = CutMode.FastCopy,
            Status = status,
        };

        using IServiceScope scope = factory.Services.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.CutJobs.Add(job);
        await db.SaveChangesAsync();

        return job;
    }

    private async Task<CutJob> LoadJobAsync(Guid jobId)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.CutJobs.AsNoTracking().SingleAsync(j => j.Id == jobId);
    }

    private sealed class StubVideoCutter(Func<Guid, string, string, CancellationToken, Task<CutResult>> cut) : IVideoCutter
    {
        public Task<CutResult> CutAsync(
            Guid jobId,
            string sourcePath,
            string outputDirectory,
            double startSeconds,
            double endSeconds,
            CutMode mode,
            CancellationToken cancellationToken = default
        ) => cut(jobId, sourcePath, outputDirectory, cancellationToken);
    }

    // Hands out the real services except IVideoCutter, which is replaced by the given stub
    private sealed class CutterOverrideScopeFactory(IServiceScopeFactory inner, IVideoCutter cutter) : IServiceScopeFactory
    {
        public IServiceScope CreateScope() => new OverrideScope(inner.CreateScope(), cutter);

        private sealed class OverrideScope(IServiceScope innerScope, IVideoCutter cutter) : IServiceScope, IServiceProvider
        {
            public IServiceProvider ServiceProvider => this;

            public object? GetService(Type serviceType) =>
                serviceType == typeof(IVideoCutter) ? cutter : innerScope.ServiceProvider.GetService(serviceType);

            public void Dispose() => innerScope.Dispose();
        }
    }
}