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
        SpliceJobQueue queue = new();

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

    private SpliceProcessingWorker CreateWorker(SpliceJobQueue? queue = null)
    {
        return new SpliceProcessingWorker(
            queue ?? new SpliceJobQueue(),
            factory.Services.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<SpliceProcessingWorker>.Instance
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
}
