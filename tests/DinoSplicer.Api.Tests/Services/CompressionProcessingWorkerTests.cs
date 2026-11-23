using DinoSplicer.Api.Data;
using DinoSplicer.Api.Exceptions;
using DinoSplicer.Api.Models;
using DinoSplicer.Api.Services;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace DinoSplicer.Api.Tests.Services;

public class CompressionProcessingWorkerTests(VideoApiFactory factory) : IClassFixture<VideoApiFactory>
{
    [Fact]
    public async Task ProcessAsync_PendingJob_SetsReadyAndRecordsOutputMetadata()
    {
        CompressionJob job = await SeedJobWithoutFileAsync();
        StubVideoCompressor compressor = new((_, _, outputDirectory, _, _, _) =>
            Task.FromResult(new CompressionResult(Path.Combine(outputDirectory, "compressed.mp4"), 9.5, 2048)));

        await CreateWorker(compressor: compressor).ProcessAsync(job.Id, CancellationToken.None);

        CompressionJob processed = await LoadJobAsync(job.Id);
        processed.Status.Should().Be(CompressionJobStatus.Ready);
        processed.ErrorMessage.Should().BeNull();
        processed.OutputFileName.Should().Be("holiday_compressed.mp4");
        processed.OutputStoragePath.Should().Be(Path.Combine(CompressionJob.OutputDirectoryName, job.Id.ToString(), "compressed.mp4"));
        processed.OutputDurationSeconds.Should().Be(9.5);
        processed.OutputSizeBytes.Should().Be(2048);
    }

    [Fact]
    public async Task ProcessAsync_PendingJob_PassesJobParametersAndPathsToCompressor()
    {
        CompressionJob job = await SeedJobWithoutFileAsync(
            format: OutputContainerFormat.Webm,
            videoCodec: VideoCodecOption.Vp9,
            crf: 31,
            targetWidth: 320,
            targetHeight: 240,
            audioBitrateKbps: 96,
            preset: EncodingPresetOption.Slow
        );
        string? receivedSourcePath = null;
        string? receivedOutputDirectory = null;
        StubVideoCompressor compressor = new((_, sourcePath, outputDirectory, _, _, _) =>
        {
            receivedSourcePath = sourcePath;
            receivedOutputDirectory = outputDirectory;
            return Task.FromResult(new CompressionResult(Path.Combine(outputDirectory, "compressed.webm"), 10.0, 2048));
        });

        await CreateWorker(compressor: compressor).ProcessAsync(job.Id, CancellationToken.None);

        compressor.ReceivedJobId.Should().Be(job.Id);
        compressor.ReceivedRequest.Should().NotBeNull();
        compressor.ReceivedRequest!.Format.Should().Be(OutputContainerFormat.Webm);
        compressor.ReceivedRequest.VideoCodec.Should().Be(VideoCodecOption.Vp9);
        compressor.ReceivedRequest.Crf.Should().Be(31);
        compressor.ReceivedRequest.TargetWidth.Should().Be(320);
        compressor.ReceivedRequest.TargetHeight.Should().Be(240);
        compressor.ReceivedRequest.AudioBitrateKbps.Should().Be(96);
        compressor.ReceivedRequest.Preset.Should().Be(EncodingPresetOption.Slow);
        compressor.ReceivedSourceDuration.Should().Be(TimeSpan.FromSeconds(10));
        receivedSourcePath.Should().Be(Path.Combine(factory.StorageDirectory, $"{job.SourceVideoId}.mp4"));
        receivedOutputDirectory.Should().Be(CompressionJob.GetOutputDirectory(factory.StorageDirectory, job.Id));
    }

    [Theory]
    [InlineData("holiday.mp4", ".mp4", "holiday_compressed.mp4")]
    [InlineData("my.trip.mp4", ".webm", "my.trip_compressed.webm")]
    public void BuildOutputFileName_NormalName_AppendsCompressedSuffix(string originalFileName, string extension, string expected)
    {
        CompressionProcessingWorker.BuildOutputFileName(originalFileName, extension).Should().Be(expected);
    }

    [Fact]
    public void BuildOutputFileName_MaxLengthName_ShortensBaseToFit255Characters()
    {
        string originalFileName = new string('a', 251) + ".mp4";

        string outputFileName = CompressionProcessingWorker.BuildOutputFileName(originalFileName, ".mp4");

        outputFileName.Length.Should().Be(255);
        outputFileName.Should().EndWith("_compressed.mp4");
    }

    [Fact]
    public async Task ProcessAsync_MaxLengthOriginalFileName_SavesReadyJobWithShortenedOutputName()
    {
        CompressionJob job = await SeedJobWithoutFileAsync(originalFileName: new string('a', 251) + ".mp4");
        StubVideoCompressor compressor = new((_, _, outputDirectory, _, _, _) =>
            Task.FromResult(new CompressionResult(Path.Combine(outputDirectory, "compressed.mp4"), 10.0, 2048)));

        await CreateWorker(compressor: compressor).ProcessAsync(job.Id, CancellationToken.None);

        CompressionJob processed = await LoadJobAsync(job.Id);
        processed.Status.Should().Be(CompressionJobStatus.Ready);
        processed.OutputFileName.Should().HaveLength(255).And.EndWith("_compressed.mp4");
    }

    [Fact]
    public async Task ProcessAsync_CancelledMidCompression_KeepsJobProcessingAndClearsProgress()
    {
        CompressionJob job = await SeedJobWithoutFileAsync();
        JobProgressTracker tracker = new();
        using CancellationTokenSource cts = new();
        StubVideoCompressor compressor = new((jobId, _, _, _, _, ct) =>
        {
            tracker.ReportProgress(jobId, 30);
            cts.Cancel();
            ct.ThrowIfCancellationRequested();
            throw new InvalidOperationException("Cancellation should have stopped the compression");
        });

        Func<Task> act = () => CreateWorker(compressor: compressor, progressTracker: tracker).ProcessAsync(job.Id, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        (await LoadJobAsync(job.Id)).Status.Should().Be(CompressionJobStatus.Processing);
        tracker.GetProgress(job.Id).Should().BeNull();
    }

    [Fact]
    public async Task ProcessAsync_InterruptedJobWithLeftoverOutput_ReplacesItAndSetsReady()
    {
        CompressionJob job = await SeedJobWithSampleVideoAsync(CompressionJobStatus.Processing);
        Directory.CreateDirectory(OutputDirectory(job.Id));
        string leftoverFile = Path.Combine(OutputDirectory(job.Id), "compressed.mp4");
        await File.WriteAllTextAsync(leftoverFile, "partial");

        await CreateWorker().ProcessAsync(job.Id, CancellationToken.None);

        CompressionJob processed = await LoadJobAsync(job.Id);
        processed.Status.Should().Be(CompressionJobStatus.Ready);
        processed.OutputDurationSeconds.Should().BeGreaterThan(0);
        new FileInfo(leftoverFile).Length.Should().BeGreaterThan("partial".Length);
    }

    [Fact]
    public async Task ProcessAsync_SuccessfulCompression_ClearsProgress()
    {
        CompressionJob job = await SeedJobWithoutFileAsync();
        JobProgressTracker tracker = new();
        StubVideoCompressor compressor = new((jobId, _, outputDirectory, _, _, _) =>
        {
            tracker.ReportProgress(jobId, 100);
            return Task.FromResult(new CompressionResult(Path.Combine(outputDirectory, "compressed.mp4"), 10.0, 2048));
        });

        await CreateWorker(compressor: compressor, progressTracker: tracker).ProcessAsync(job.Id, CancellationToken.None);

        tracker.GetProgress(job.Id).Should().BeNull();
    }

    [Fact]
    public async Task ProcessAsync_CompressorThrowsMediaProcessingException_SetsFailedWithMessageCleansUpAndClearsProgress()
    {
        CompressionJob job = await SeedJobWithoutFileAsync();
        JobProgressTracker tracker = new();
        StubVideoCompressor compressor = new((jobId, _, outputDirectory, _, _, _) =>
        {
            tracker.ReportProgress(jobId, 40);
            Directory.CreateDirectory(outputDirectory);
            File.WriteAllText(Path.Combine(outputDirectory, "compressed.mp4"), "partial");
            throw new MediaProcessingException("Custom compression error message.");
        });

        await CreateWorker(compressor: compressor, progressTracker: tracker).ProcessAsync(job.Id, CancellationToken.None);

        CompressionJob processed = await LoadJobAsync(job.Id);
        processed.Status.Should().Be(CompressionJobStatus.Failed);
        processed.ErrorMessage.Should().Be("Custom compression error message.");
        processed.OutputFileName.Should().BeNull();
        processed.OutputStoragePath.Should().BeNull();
        Directory.Exists(OutputDirectory(job.Id)).Should().BeFalse();
        tracker.GetProgress(job.Id).Should().BeNull();
    }

    [Fact]
    public async Task ProcessAsync_MediaProcessingExceptionWithLongMessage_TruncatesErrorMessageTo500Characters()
    {
        CompressionJob job = await SeedJobWithoutFileAsync();
        string longMessage = new('x', 600);
        StubVideoCompressor compressor = new((_, _, _, _, _, _) => throw new MediaProcessingException(longMessage));

        await CreateWorker(compressor: compressor).ProcessAsync(job.Id, CancellationToken.None);

        CompressionJob processed = await LoadJobAsync(job.Id);
        processed.Status.Should().Be(CompressionJobStatus.Failed);
        processed.ErrorMessage.Should().HaveLength(500);
    }

    [Fact]
    public async Task ProcessAsync_CompressorThrowsUnexpectedException_SetsFailedWithGenericMessage()
    {
        CompressionJob job = await SeedJobWithoutFileAsync();
        StubVideoCompressor compressor = new((_, _, _, _, _, _) => throw new InvalidOperationException("internal details"));

        await CreateWorker(compressor: compressor).ProcessAsync(job.Id, CancellationToken.None);

        CompressionJob processed = await LoadJobAsync(job.Id);
        processed.Status.Should().Be(CompressionJobStatus.Failed);
        processed.ErrorMessage.Should().Be(CompressionProcessingWorker.GenericFailureMessage);
    }

    [Theory]
    [InlineData(CompressionJobStatus.Ready)]
    [InlineData(CompressionJobStatus.Failed)]
    public async Task ProcessAsync_JobAlreadyFinished_SkipsCompressorAndKeepsStatus(CompressionJobStatus status)
    {
        CompressionJob job = await SeedJobWithoutFileAsync(status);
        bool compressorCalled = false;
        StubVideoCompressor compressor = new((_, _, outputDirectory, _, _, _) =>
        {
            compressorCalled = true;
            return Task.FromResult(new CompressionResult(Path.Combine(outputDirectory, "compressed.mp4"), 10.0, 2048));
        });

        await CreateWorker(compressor: compressor).ProcessAsync(job.Id, CancellationToken.None);

        compressorCalled.Should().BeFalse();
        (await LoadJobAsync(job.Id)).Status.Should().Be(status);
    }

    [Fact]
    public async Task ProcessAsync_JobNoLongerExists_DoesNothing()
    {
        bool compressorCalled = false;
        StubVideoCompressor compressor = new((_, _, outputDirectory, _, _, _) =>
        {
            compressorCalled = true;
            return Task.FromResult(new CompressionResult(Path.Combine(outputDirectory, "compressed.mp4"), 10.0, 2048));
        });

        Func<Task> act = () => CreateWorker(compressor: compressor).ProcessAsync(Guid.NewGuid(), CancellationToken.None);

        await act.Should().NotThrowAsync();
        compressorCalled.Should().BeFalse();
    }

    [Fact]
    public async Task ProcessAsync_HoldsProcessingLeaseWhileCompressing_AndReleasesItWhenFinished()
    {
        CompressionJob job = await SeedJobWithoutFileAsync();
        TaskCompletionSource compressorStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource releaseCompressor = new(TaskCreationOptions.RunContinuationsAsynchronously);
        VideoAccessCoordinator coordinator = new();
        StubVideoCompressor compressor = new(async (_, _, outputDirectory, _, _, ct) =>
        {
            compressorStarted.TrySetResult();
            await releaseCompressor.Task.WaitAsync(ct);
            return new CompressionResult(Path.Combine(outputDirectory, "compressed.mp4"), 10.0, 2048);
        });

        Task processTask = CreateWorker(compressor: compressor, accessCoordinator: coordinator)
            .ProcessAsync(job.Id, CancellationToken.None);
        await compressorStarted.Task;

        // While compressing, the source video cannot be deleted
        coordinator.TryAcquireDelete(job.SourceVideoId).Should().BeFalse();

        releaseCompressor.SetResult();
        await processTask;

        coordinator.TryAcquireDelete(job.SourceVideoId).Should().BeTrue();
        coordinator.CompleteDelete(job.SourceVideoId);
    }

    [Fact]
    public async Task ProcessAsync_SourceVideoIsBeingDeleted_SetsFailedAndSkipsCompression()
    {
        CompressionJob job = await SeedJobWithoutFileAsync();
        VideoAccessCoordinator coordinator = new();
        coordinator.TryAcquireDelete(job.SourceVideoId).Should().BeTrue();
        bool compressorCalled = false;
        StubVideoCompressor compressor = new((_, _, outputDirectory, _, _, _) =>
        {
            compressorCalled = true;
            return Task.FromResult(new CompressionResult(Path.Combine(outputDirectory, "compressed.mp4"), 10.0, 2048));
        });

        await CreateWorker(compressor: compressor, accessCoordinator: coordinator).ProcessAsync(job.Id, CancellationToken.None);

        coordinator.CompleteDelete(job.SourceVideoId);
        compressorCalled.Should().BeFalse();
        CompressionJob processed = await LoadJobAsync(job.Id);
        processed.Status.Should().Be(CompressionJobStatus.Failed);
        processed.ErrorMessage.Should().Be(CompressionProcessingWorker.SourceBeingDeletedMessage);
    }

    [Fact]
    public async Task ProcessAsync_SourceVideoDeletedBeforeProcessing_SkipsCompressionBecauseJobIsCascadeDeleted()
    {
        CompressionJob job = await SeedJobWithoutFileAsync();
        using (IServiceScope scope = factory.Services.CreateScope())
        {
            AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Videos.Where(v => v.Id == job.SourceVideoId).ExecuteDeleteAsync();
        }

        bool compressorCalled = false;
        StubVideoCompressor compressor = new((_, _, outputDirectory, _, _, _) =>
        {
            compressorCalled = true;
            return Task.FromResult(new CompressionResult(Path.Combine(outputDirectory, "compressed.mp4"), 10.0, 2048));
        });

        Func<Task> act = () => CreateWorker(compressor: compressor).ProcessAsync(job.Id, CancellationToken.None);

        await act.Should().NotThrowAsync();
        compressorCalled.Should().BeFalse();
        using IServiceScope checkScope = factory.Services.CreateScope();
        AppDbContext checkDb = checkScope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await checkDb.CompressionJobs.AnyAsync(j => j.Id == job.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task ProcessAsync_RealCompressorOnSampleVideo_SetsReadyAndWritesCompressedVideo()
    {
        CompressionJob job = await SeedJobWithSampleVideoAsync();

        await CreateWorker().ProcessAsync(job.Id, CancellationToken.None);

        CompressionJob processed = await LoadJobAsync(job.Id);
        processed.Status.Should().Be(CompressionJobStatus.Ready);
        processed.OutputFileName.Should().Be("holiday_compressed.mp4");
        processed.OutputDurationSeconds.Should().BeGreaterThan(0);
        processed.OutputSizeBytes.Should().BeGreaterThan(0);
        File.Exists(Path.Combine(factory.StorageDirectory, processed.OutputStoragePath!)).Should().BeTrue();
    }

    [Theory]
    [InlineData(CompressionJobStatus.Pending)]
    [InlineData(CompressionJobStatus.Processing)]
    public async Task RecoverPendingJobsAsync_UnfinishedJob_RequeuesIt(CompressionJobStatus status)
    {
        CompressionJob job = await SeedJobWithoutFileAsync(status);
        BackgroundJobQueue<CompressionJob> queue = new();

        await CreateWorker(queue).RecoverPendingJobsAsync(CancellationToken.None);

        (await ReadQueuedIdsAsync(queue)).Should().Contain(job.Id);
    }

    [Theory]
    [InlineData(CompressionJobStatus.Ready)]
    [InlineData(CompressionJobStatus.Failed)]
    public async Task RecoverPendingJobsAsync_FinishedJob_IsNotRequeued(CompressionJobStatus status)
    {
        CompressionJob job = await SeedJobWithoutFileAsync(status);
        BackgroundJobQueue<CompressionJob> queue = new();

        await CreateWorker(queue).RecoverPendingJobsAsync(CancellationToken.None);

        (await ReadQueuedIdsAsync(queue)).Should().NotContain(job.Id);
    }

    private CompressionProcessingWorker CreateWorker(
        IBackgroundJobQueue<CompressionJob>? queue = null,
        IVideoCompressor? compressor = null,
        IVideoAccessCoordinator? accessCoordinator = null,
        IJobProgressTracker? progressTracker = null
    )
    {
        IServiceScopeFactory scopeFactory = factory.Services.GetRequiredService<IServiceScopeFactory>();
        if (compressor is not null)
        {
            scopeFactory = new CompressorOverrideScopeFactory(scopeFactory, compressor);
        }

        return new CompressionProcessingWorker(
            queue ?? new BackgroundJobQueue<CompressionJob>(),
            accessCoordinator ?? new VideoAccessCoordinator(),
            progressTracker ?? new JobProgressTracker(),
            scopeFactory,
            NullLogger<CompressionProcessingWorker>.Instance
        );
    }

    // A fresh queue holds only what recovery added. Jobs left unfinished by earlier tests in this class are
    // included too, so read everything until the queue stays empty for a moment
    private static async Task<List<Guid>> ReadQueuedIdsAsync(BackgroundJobQueue<CompressionJob> queue)
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
        return CompressionJob.GetOutputDirectory(factory.StorageDirectory, jobId);
    }

    private async Task<CompressionJob> SeedJobWithoutFileAsync(
        CompressionJobStatus status = CompressionJobStatus.Pending,
        string originalFileName = "holiday.mp4",
        OutputContainerFormat format = OutputContainerFormat.Mp4,
        VideoCodecOption videoCodec = VideoCodecOption.H264,
        int crf = CompressionRequest.DefaultCrf,
        int? targetWidth = null,
        int? targetHeight = null,
        int? audioBitrateKbps = null,
        EncodingPresetOption preset = EncodingPresetOption.Ultrafast
    )
    {
        Video video = new()
        {
            OriginalFileName = originalFileName,
            ContentType = "video/mp4",
            SizeBytes = 4096,
            DurationSeconds = 10,
            Status = VideoStatus.Ready,
        };
        video.FileName = $"{video.Id}.mp4";

        return await SaveJobAsync(
            video,
            status,
            format,
            videoCodec,
            crf,
            targetWidth,
            targetHeight,
            audioBitrateKbps,
            preset
        );
    }

    private async Task<CompressionJob> SeedJobWithSampleVideoAsync(CompressionJobStatus status = CompressionJobStatus.Pending)
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

        return await SaveJobAsync(video, status);
    }

    private async Task<CompressionJob> SaveJobAsync(
        Video video,
        CompressionJobStatus status,
        OutputContainerFormat format = OutputContainerFormat.Mp4,
        VideoCodecOption videoCodec = VideoCodecOption.H264,
        int crf = CompressionRequest.DefaultCrf,
        int? targetWidth = null,
        int? targetHeight = null,
        int? audioBitrateKbps = null,
        EncodingPresetOption preset = EncodingPresetOption.Ultrafast
    )
    {
        CompressionJob job = new()
        {
            SourceVideo = video,
            Format = format,
            VideoCodec = videoCodec,
            Crf = crf,
            TargetWidth = targetWidth,
            TargetHeight = targetHeight,
            AudioBitrateKbps = audioBitrateKbps,
            Preset = preset,
            Status = status,
        };

        using IServiceScope scope = factory.Services.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.CompressionJobs.Add(job);
        await db.SaveChangesAsync();

        return job;
    }

    private async Task<CompressionJob> LoadJobAsync(Guid jobId)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.CompressionJobs.AsNoTracking().SingleAsync(j => j.Id == jobId);
    }

    private sealed class StubVideoCompressor(
        Func<Guid, string, string, CompressionRequest, TimeSpan?, CancellationToken, Task<CompressionResult>> compress
    ) : IVideoCompressor
    {
        public Guid? ReceivedJobId { get; private set; }
        public CompressionRequest? ReceivedRequest { get; private set; }
        public TimeSpan? ReceivedSourceDuration { get; private set; }

        public Task<CompressionResult> CompressAsync(
            Guid jobId,
            string sourcePath,
            string outputDirectory,
            CompressionRequest request,
            TimeSpan? sourceDuration = null,
            CancellationToken cancellationToken = default
        )
        {
            ReceivedJobId = jobId;
            ReceivedRequest = request;
            ReceivedSourceDuration = sourceDuration;
            return compress(jobId, sourcePath, outputDirectory, request, sourceDuration, cancellationToken);
        }
    }

    // Hands out the real services except IVideoCompressor, which is replaced by the given stub
    private sealed class CompressorOverrideScopeFactory(IServiceScopeFactory inner, IVideoCompressor compressor) : IServiceScopeFactory
    {
        public IServiceScope CreateScope() => new OverrideScope(inner.CreateScope(), compressor);

        private sealed class OverrideScope(IServiceScope innerScope, IVideoCompressor compressor) : IServiceScope, IServiceProvider
        {
            public IServiceProvider ServiceProvider => this;

            public object? GetService(Type serviceType) =>
                serviceType == typeof(IVideoCompressor) ? compressor : innerScope.ServiceProvider.GetService(serviceType);

            public void Dispose() => innerScope.Dispose();
        }
    }
}
