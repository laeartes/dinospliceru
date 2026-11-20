using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;

using DinoSplicer.Api.Data;
using DinoSplicer.Api.Models;
using DinoSplicer.Api.Services;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DinoSplicer.Api.Tests.Controllers;

public class CutJobsControllerTests(VideoApiFactory factory) : IClassFixture<VideoApiFactory>
{
    [Fact]
    public async Task GetById_PendingJob_Returns200WithZeroProgressAndParameters()
    {
        Video video = await SeedReadyVideoAsync();
        CutJob job = await SeedJobAsync(video.Id, CutJobStatus.Pending);
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(JobUrl(job.Id));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        CutJobResponse? body = await response.Content.ReadFromJsonAsync<CutJobResponse>();
        body.Should().BeEquivalentTo(new CutJobResponse(
            job.Id,
            nameof(CutMode.FastCopy),
            1.5,
            6.5,
            nameof(CutJobStatus.Pending),
            0,
            null,
            null,
            null,
            null
        ));
    }

    [Fact]
    public async Task GetById_ProcessingJob_ReturnsLiveProgressFromTracker()
    {
        Video video = await SeedReadyVideoAsync();
        CutJob job = await SeedJobAsync(video.Id, CutJobStatus.Processing);
        IJobProgressTracker tracker = factory.Services.GetRequiredService<IJobProgressTracker>();
        HttpClient client = factory.CreateClient();

        CutJobResponse initial = (await client.GetFromJsonAsync<CutJobResponse>(JobUrl(job.Id)))!;
        tracker.ReportProgress(job.Id, 42);
        CutJobResponse mid = (await client.GetFromJsonAsync<CutJobResponse>(JobUrl(job.Id)))!;
        tracker.ReportProgress(job.Id, 78);
        CutJobResponse late = (await client.GetFromJsonAsync<CutJobResponse>(JobUrl(job.Id)))!;
        tracker.Remove(job.Id);

        initial.ProgressPercent.Should().Be(0);
        mid.Status.Should().Be(nameof(CutJobStatus.Processing));
        mid.ProgressPercent.Should().Be(42);
        late.ProgressPercent.Should().Be(78);
    }

    [Fact]
    public async Task GetById_ReadyJob_Returns200WithFullProgressAndOutputDetails()
    {
        Video video = await SeedReadyVideoAsync();
        CutJob job = await SeedJobAsync(
            video.Id,
            CutJobStatus.Ready,
            outputFileName: "holiday_cut.mp4",
            outputDurationSeconds: 5.0,
            outputSizeBytes: 2048
        );
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(JobUrl(job.Id));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        CutJobResponse? body = await response.Content.ReadFromJsonAsync<CutJobResponse>();
        body.Should().BeEquivalentTo(new CutJobResponse(
            job.Id,
            nameof(CutMode.FastCopy),
            1.5,
            6.5,
            nameof(CutJobStatus.Ready),
            100,
            null,
            "holiday_cut.mp4",
            5.0,
            2048
        ));
    }

    [Fact]
    public async Task GetById_FailedJob_Returns200WithErrorAndNullProgress()
    {
        Video video = await SeedReadyVideoAsync();
        CutJob job = await SeedJobAsync(video.Id, CutJobStatus.Failed, errorMessage: "FFmpeg failed to cut video.");
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(JobUrl(job.Id));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        CutJobResponse? body = await response.Content.ReadFromJsonAsync<CutJobResponse>();
        body.Should().BeEquivalentTo(new CutJobResponse(
            job.Id,
            nameof(CutMode.FastCopy),
            1.5,
            6.5,
            nameof(CutJobStatus.Failed),
            null,
            "FFmpeg failed to cut video.",
            null,
            null,
            null
        ));
    }

    [Fact]
    public async Task GetById_MissingId_Returns404()
    {
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(JobUrl(Guid.NewGuid()));

        await response.ShouldBeApiErrorAsync(HttpStatusCode.NotFound, ApiErrorCodes.CutJobNotFound);
    }

    [Fact]
    public async Task Download_ReadyJob_ReturnsFileWithAttachmentHeaders()
    {
        byte[] content = Encoding.UTF8.GetBytes("trimmed clip bytes");
        Video video = await SeedReadyVideoAsync();
        CutJob job = await SeedJobAsync(video.Id, CutJobStatus.Ready, fileContent: content);
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(DownloadUrl(job.Id));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("video/mp4");
        response.Content.Headers.ContentLength.Should().Be(content.Length);
        ContentDispositionHeaderValue disposition = response.Content.Headers.ContentDisposition!;
        disposition.DispositionType.Should().Be("attachment");
        disposition.FileName!.Trim('"').Should().Be("holiday_cut.mp4");
        (await response.Content.ReadAsByteArrayAsync()).Should().Equal(content);
    }

    [Fact]
    public async Task Download_ReadyJobWithRangeHeader_Returns206PartialContent()
    {
        byte[] content = Encoding.UTF8.GetBytes("0123456789abcdef");
        Video video = await SeedReadyVideoAsync();
        CutJob job = await SeedJobAsync(video.Id, CutJobStatus.Ready, fileContent: content);
        HttpClient client = factory.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Get, DownloadUrl(job.Id));
        request.Headers.Range = new RangeHeaderValue(2, 5);

        HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.PartialContent);
        response.Content.Headers.ContentRange.Should().BeEquivalentTo(new ContentRangeHeaderValue(2, 5, content.Length));
        response.Content.Headers.ContentLength.Should().Be(4);
        (await response.Content.ReadAsByteArrayAsync()).Should().Equal(content[2..6]);
    }

    [Fact]
    public async Task Download_ReadyJobWithNonAsciiFileName_EncodesNameInDisposition()
    {
        const string fileName = "släpp vidéo_cut.mp4";
        Video video = await SeedReadyVideoAsync();
        CutJob job = await SeedJobAsync(video.Id, CutJobStatus.Ready, outputFileName: fileName, fileContent: [1, 2, 3]);
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(DownloadUrl(job.Id));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentDisposition!.FileNameStar.Should().Be(fileName);
    }

    [Fact]
    public async Task Download_ReadyJobWithUnknownExtension_FallsBackToOctetStream()
    {
        Video video = await SeedReadyVideoAsync();
        CutJob job = await SeedJobAsync(
            video.Id,
            CutJobStatus.Ready,
            outputFileName: "holiday_cut.unknownext",
            fileContent: [1, 2, 3, 4]
        );
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(DownloadUrl(job.Id));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/octet-stream");
    }

    [Theory]
    [InlineData(CutJobStatus.Pending)]
    [InlineData(CutJobStatus.Processing)]
    [InlineData(CutJobStatus.Failed)]
    public async Task Download_JobNotReady_Returns409AndNoFile(CutJobStatus status)
    {
        Video video = await SeedReadyVideoAsync();
        CutJob job = await SeedJobAsync(video.Id, status, fileContent: Encoding.UTF8.GetBytes("clip bytes"));
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(DownloadUrl(job.Id));

        await response.ShouldBeApiErrorAsync(HttpStatusCode.Conflict, ApiErrorCodes.CutJobNotReady);
        response.Content.Headers.ContentDisposition.Should().BeNull();
    }

    [Fact]
    public async Task Download_MissingId_Returns404()
    {
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(DownloadUrl(Guid.NewGuid()));

        await response.ShouldBeApiErrorAsync(HttpStatusCode.NotFound, ApiErrorCodes.CutJobNotFound);
    }

    [Fact]
    public async Task Download_ReadyJobWithoutFileOnDisk_Returns404()
    {
        Video video = await SeedReadyVideoAsync();
        CutJob job = await SeedJobAsync(video.Id, CutJobStatus.Ready, outputFileName: "holiday_cut.mp4");
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(DownloadUrl(job.Id));

        await response.ShouldBeApiErrorAsync(HttpStatusCode.NotFound, ApiErrorCodes.CutOutputFileMissing);
    }

    [Fact]
    public async Task Download_ReadyJobWithoutOutputMetadata_Returns404()
    {
        Video video = await SeedReadyVideoAsync();
        CutJob job = new()
        {
            SourceVideoId = video.Id,
            StartSeconds = 1.0,
            EndSeconds = 5.0,
            Mode = CutMode.FastCopy,
            Status = CutJobStatus.Ready,
        };
        await SaveAsync(job);
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(DownloadUrl(job.Id));

        await response.ShouldBeApiErrorAsync(HttpStatusCode.NotFound, ApiErrorCodes.CutOutputFileMissing);
    }

    [Fact]
    public async Task Delete_ReadyJob_Returns204AndRemovesRowDirectoryAndProgress()
    {
        Video video = await SeedReadyVideoAsync();
        CutJob job = await SeedJobAsync(video.Id, CutJobStatus.Ready, fileContent: [1, 2, 3, 4]);
        IJobProgressTracker tracker = factory.Services.GetRequiredService<IJobProgressTracker>();
        tracker.ReportProgress(job.Id, 100);
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.DeleteAsync(JobUrl(job.Id));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        tracker.GetProgress(job.Id).Should().BeNull();
        Directory.Exists(CutJob.GetOutputDirectory(factory.StorageDirectory, job.Id)).Should().BeFalse();
        using IServiceScope scope = factory.Services.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.CutJobs.AnyAsync(j => j.Id == job.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task Delete_ReadyJob_KeepsSourceVideoAndFile()
    {
        Video video = await SeedReadyVideoAsync(withSourceFile: true);
        CutJob job = await SeedJobAsync(video.Id, CutJobStatus.Ready, fileContent: [1, 2, 3, 4]);
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.DeleteAsync(JobUrl(job.Id));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        using IServiceScope scope = factory.Services.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.Videos.AnyAsync(v => v.Id == video.Id)).Should().BeTrue();
        File.Exists(Path.Combine(factory.StorageDirectory, video.FileName)).Should().BeTrue();
    }

    [Fact]
    public async Task Delete_ProcessingJob_Returns409AndKeepsJobAndFiles()
    {
        Video video = await SeedReadyVideoAsync();
        CutJob job = await SeedJobAsync(video.Id, CutJobStatus.Processing, fileContent: [1, 2, 3]);
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.DeleteAsync(JobUrl(job.Id));

        await response.ShouldBeApiErrorAsync(HttpStatusCode.Conflict, ApiErrorCodes.CutJobProcessing);
        Directory.Exists(CutJob.GetOutputDirectory(factory.StorageDirectory, job.Id)).Should().BeTrue();
        using IServiceScope scope = factory.Services.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.CutJobs.AnyAsync(j => j.Id == job.Id)).Should().BeTrue();
    }

    [Theory]
    [InlineData(CutJobStatus.Pending)]
    [InlineData(CutJobStatus.Failed)]
    public async Task Delete_PendingOrFailedJob_Returns204AndRemovesJob(CutJobStatus status)
    {
        Video video = await SeedReadyVideoAsync();
        CutJob job = await SeedJobAsync(video.Id, status);
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.DeleteAsync(JobUrl(job.Id));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        using IServiceScope scope = factory.Services.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.CutJobs.AnyAsync(j => j.Id == job.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task Delete_FolderWithStrayFile_Returns204AndRemovesFolder()
    {
        Video video = await SeedReadyVideoAsync();
        CutJob job = await SeedJobAsync(video.Id, CutJobStatus.Ready, fileContent: [1, 2, 3]);
        string outputDirectory = CutJob.GetOutputDirectory(factory.StorageDirectory, job.Id);
        await File.WriteAllTextAsync(Path.Combine(outputDirectory, "stray.tmp"), "left behind");
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.DeleteAsync(JobUrl(job.Id));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        Directory.Exists(outputDirectory).Should().BeFalse();
    }

    [Fact]
    public async Task Delete_MissingId_Returns404()
    {
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.DeleteAsync(JobUrl(Guid.NewGuid()));

        await response.ShouldBeApiErrorAsync(HttpStatusCode.NotFound, ApiErrorCodes.CutJobNotFound);
    }

    private static string JobUrl(Guid jobId)
    {
        return $"/api/cut-jobs/{jobId}";
    }

    private static string DownloadUrl(Guid jobId)
    {
        return $"/api/cut-jobs/{jobId}/download";
    }

    private async Task<Video> SeedReadyVideoAsync(bool withSourceFile = false)
    {
        Video video = new()
        {
            OriginalFileName = "holiday.mp4",
            ContentType = "video/mp4",
            SizeBytes = 1024,
            DurationSeconds = 12.0,
            Status = VideoStatus.Ready,
        };
        video.FileName = $"{video.Id}.mp4";

        if (withSourceFile)
        {
            Directory.CreateDirectory(factory.StorageDirectory);
            await File.WriteAllBytesAsync(Path.Combine(factory.StorageDirectory, video.FileName), [9, 8, 7]);
        }

        await SaveAsync(video);
        return video;
    }

    private async Task<CutJob> SeedJobAsync(
        Guid videoId,
        CutJobStatus status,
        string? outputFileName = null,
        double? outputDurationSeconds = null,
        long? outputSizeBytes = null,
        string? errorMessage = null,
        byte[]? fileContent = null
    )
    {
        string? resolvedFileName = outputFileName ?? (fileContent is not null ? "holiday_cut.mp4" : null);
        CutJob job = new()
        {
            SourceVideoId = videoId,
            StartSeconds = 1.5,
            EndSeconds = 6.5,
            Mode = CutMode.FastCopy,
            Status = status,
            OutputFileName = resolvedFileName,
            OutputDurationSeconds = outputDurationSeconds,
            OutputSizeBytes = outputSizeBytes ?? fileContent?.Length,
            ErrorMessage = errorMessage,
        };

        if (resolvedFileName is not null)
        {
            job.OutputStoragePath = Path.Combine(CutJob.OutputDirectoryName, job.Id.ToString(), "clip.mp4");
        }

        if (fileContent is not null && job.OutputStoragePath is not null)
        {
            string filePath = Path.Combine(factory.StorageDirectory, job.OutputStoragePath);
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            await File.WriteAllBytesAsync(filePath, fileContent);
        }

        await SaveAsync(job);
        return job;
    }

    private async Task SaveAsync(object entity)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Add(entity);
        await db.SaveChangesAsync();
    }
}
