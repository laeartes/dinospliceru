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

public class CompressionJobsControllerTests(VideoApiFactory factory) : IClassFixture<VideoApiFactory>
{
    [Fact]
    public async Task GetById_PendingJob_Returns200WithZeroProgressAndParameters()
    {
        Video video = await SeedReadyVideoAsync();
        CompressionJob job = await SeedJobAsync(video.Id, CompressionJobStatus.Pending);
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(JobUrl(job.Id));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        CompressionJobResponse? body = await response.Content.ReadFromJsonAsync<CompressionJobResponse>();
        body.Should().BeEquivalentTo(ExpectedResponse(job, nameof(CompressionJobStatus.Pending), 0));
    }

    [Fact]
    public async Task GetById_ProcessingJob_ReturnsLiveProgressFromTracker()
    {
        Video video = await SeedReadyVideoAsync();
        CompressionJob job = await SeedJobAsync(video.Id, CompressionJobStatus.Processing);
        IJobProgressTracker tracker = factory.Services.GetRequiredService<IJobProgressTracker>();
        HttpClient client = factory.CreateClient();

        CompressionJobResponse initial = (await client.GetFromJsonAsync<CompressionJobResponse>(JobUrl(job.Id)))!;
        tracker.ReportProgress(job.Id, 42);
        CompressionJobResponse mid = (await client.GetFromJsonAsync<CompressionJobResponse>(JobUrl(job.Id)))!;
        tracker.ReportProgress(job.Id, 78);
        CompressionJobResponse late = (await client.GetFromJsonAsync<CompressionJobResponse>(JobUrl(job.Id)))!;
        tracker.Remove(job.Id);

        initial.ProgressPercent.Should().Be(0);
        mid.Status.Should().Be(nameof(CompressionJobStatus.Processing));
        mid.ProgressPercent.Should().Be(42);
        late.ProgressPercent.Should().Be(78);
    }

    [Fact]
    public async Task GetById_ReadyJob_Returns200WithFullProgressAndOutputDetails()
    {
        Video video = await SeedReadyVideoAsync();
        CompressionJob job = await SeedJobAsync(
            video.Id,
            CompressionJobStatus.Ready,
            outputFileName: "holiday_compressed.mp4",
            outputDurationSeconds: 5.0,
            outputSizeBytes: 2048
        );
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(JobUrl(job.Id));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        CompressionJobResponse? body = await response.Content.ReadFromJsonAsync<CompressionJobResponse>();
        body.Should().BeEquivalentTo(ExpectedResponse(
            job,
            nameof(CompressionJobStatus.Ready),
            100,
            outputFileName: "holiday_compressed.mp4",
            outputDurationSeconds: 5.0,
            outputSizeBytes: 2048
        ));
    }

    [Fact]
    public async Task GetById_FailedJob_Returns200WithErrorAndNullProgress()
    {
        Video video = await SeedReadyVideoAsync();
        CompressionJob job = await SeedJobAsync(
            video.Id,
            CompressionJobStatus.Failed,
            errorMessage: "FFmpeg failed to compress video."
        );
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(JobUrl(job.Id));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        CompressionJobResponse? body = await response.Content.ReadFromJsonAsync<CompressionJobResponse>();
        body.Should().BeEquivalentTo(ExpectedResponse(
            job,
            nameof(CompressionJobStatus.Failed),
            null,
            error: "FFmpeg failed to compress video."
        ));
    }

    [Fact]
    public async Task GetById_MissingId_Returns404()
    {
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(JobUrl(Guid.NewGuid()));

        await response.ShouldBeApiErrorAsync(HttpStatusCode.NotFound, ApiErrorCodes.CompressionJobNotFound);
    }

    [Fact]
    public async Task Download_ReadyJob_ReturnsFileWithAttachmentHeaders()
    {
        byte[] content = Encoding.UTF8.GetBytes("compressed video bytes");
        Video video = await SeedReadyVideoAsync();
        CompressionJob job = await SeedJobAsync(video.Id, CompressionJobStatus.Ready, fileContent: content);
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(DownloadUrl(job.Id));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("video/mp4");
        response.Content.Headers.ContentLength.Should().Be(content.Length);
        ContentDispositionHeaderValue disposition = response.Content.Headers.ContentDisposition!;
        disposition.DispositionType.Should().Be("attachment");
        disposition.FileName!.Trim('"').Should().Be("holiday_compressed.mp4");
        (await response.Content.ReadAsByteArrayAsync()).Should().Equal(content);
    }

    [Fact]
    public async Task Download_ReadyWebmJob_ReturnsWebmContentType()
    {
        Video video = await SeedReadyVideoAsync();
        CompressionJob job = await SeedJobAsync(
            video.Id,
            CompressionJobStatus.Ready,
            outputFileName: "holiday_compressed.webm",
            fileContent: [1, 2, 3, 4]
        );
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(DownloadUrl(job.Id));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("video/webm");
        response.Content.Headers.ContentDisposition!.FileName!.Trim('"').Should().Be("holiday_compressed.webm");
    }

    [Fact]
    public async Task Download_ReadyJobWithRangeHeader_Returns206PartialContent()
    {
        byte[] content = Encoding.UTF8.GetBytes("0123456789abcdef");
        Video video = await SeedReadyVideoAsync();
        CompressionJob job = await SeedJobAsync(video.Id, CompressionJobStatus.Ready, fileContent: content);
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
    public async Task Download_ReadyJobWithUnknownExtension_FallsBackToOctetStream()
    {
        Video video = await SeedReadyVideoAsync();
        CompressionJob job = await SeedJobAsync(
            video.Id,
            CompressionJobStatus.Ready,
            outputFileName: "holiday_compressed.unknownext",
            fileContent: [1, 2, 3, 4]
        );
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(DownloadUrl(job.Id));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/octet-stream");
    }

    [Theory]
    [InlineData(CompressionJobStatus.Pending)]
    [InlineData(CompressionJobStatus.Processing)]
    [InlineData(CompressionJobStatus.Failed)]
    public async Task Download_JobNotReady_Returns409AndNoFile(CompressionJobStatus status)
    {
        Video video = await SeedReadyVideoAsync();
        CompressionJob job = await SeedJobAsync(video.Id, status, fileContent: Encoding.UTF8.GetBytes("video bytes"));
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(DownloadUrl(job.Id));

        await response.ShouldBeApiErrorAsync(HttpStatusCode.Conflict, ApiErrorCodes.CompressionJobNotReady);
        response.Content.Headers.ContentDisposition.Should().BeNull();
    }

    [Fact]
    public async Task Download_MissingId_Returns404()
    {
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(DownloadUrl(Guid.NewGuid()));

        await response.ShouldBeApiErrorAsync(HttpStatusCode.NotFound, ApiErrorCodes.CompressionJobNotFound);
    }

    [Fact]
    public async Task Download_ReadyJobWithoutFileOnDisk_Returns404()
    {
        Video video = await SeedReadyVideoAsync();
        CompressionJob job = await SeedJobAsync(video.Id, CompressionJobStatus.Ready, outputFileName: "holiday_compressed.mp4");
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(DownloadUrl(job.Id));

        await response.ShouldBeApiErrorAsync(HttpStatusCode.NotFound, ApiErrorCodes.CompressionOutputFileMissing);
    }

    [Fact]
    public async Task Download_ReadyJobWithoutOutputMetadata_Returns404()
    {
        Video video = await SeedReadyVideoAsync();
        CompressionJob job = new()
        {
            SourceVideoId = video.Id,
            Format = OutputContainerFormat.Mp4,
            VideoCodec = VideoCodecOption.H264,
            Crf = 23,
            Preset = EncodingPresetOption.Medium,
            Status = CompressionJobStatus.Ready,
        };
        await SaveAsync(job);
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(DownloadUrl(job.Id));

        await response.ShouldBeApiErrorAsync(HttpStatusCode.NotFound, ApiErrorCodes.CompressionOutputFileMissing);
    }

    [Fact]
    public async Task Delete_ReadyJob_Returns204AndRemovesRowDirectoryAndProgress()
    {
        Video video = await SeedReadyVideoAsync();
        CompressionJob job = await SeedJobAsync(video.Id, CompressionJobStatus.Ready, fileContent: [1, 2, 3, 4]);
        IJobProgressTracker tracker = factory.Services.GetRequiredService<IJobProgressTracker>();
        tracker.ReportProgress(job.Id, 100);
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.DeleteAsync(JobUrl(job.Id));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        tracker.GetProgress(job.Id).Should().BeNull();
        Directory.Exists(CompressionJob.GetOutputDirectory(factory.StorageDirectory, job.Id)).Should().BeFalse();
        using IServiceScope scope = factory.Services.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.CompressionJobs.AnyAsync(j => j.Id == job.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task Delete_ReadyJob_KeepsSourceVideoAndFile()
    {
        Video video = await SeedReadyVideoAsync(withSourceFile: true);
        CompressionJob job = await SeedJobAsync(video.Id, CompressionJobStatus.Ready, fileContent: [1, 2, 3, 4]);
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
        CompressionJob job = await SeedJobAsync(video.Id, CompressionJobStatus.Processing, fileContent: [1, 2, 3]);
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.DeleteAsync(JobUrl(job.Id));

        await response.ShouldBeApiErrorAsync(HttpStatusCode.Conflict, ApiErrorCodes.CompressionJobProcessing);
        Directory.Exists(CompressionJob.GetOutputDirectory(factory.StorageDirectory, job.Id)).Should().BeTrue();
        using IServiceScope scope = factory.Services.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.CompressionJobs.AnyAsync(j => j.Id == job.Id)).Should().BeTrue();
    }

    [Theory]
    [InlineData(CompressionJobStatus.Pending)]
    [InlineData(CompressionJobStatus.Failed)]
    public async Task Delete_PendingOrFailedJob_Returns204AndRemovesJob(CompressionJobStatus status)
    {
        Video video = await SeedReadyVideoAsync();
        CompressionJob job = await SeedJobAsync(video.Id, status);
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.DeleteAsync(JobUrl(job.Id));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        using IServiceScope scope = factory.Services.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.CompressionJobs.AnyAsync(j => j.Id == job.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task Delete_FolderWithStrayFile_Returns204AndRemovesFolder()
    {
        Video video = await SeedReadyVideoAsync();
        CompressionJob job = await SeedJobAsync(video.Id, CompressionJobStatus.Ready, fileContent: [1, 2, 3]);
        string outputDirectory = CompressionJob.GetOutputDirectory(factory.StorageDirectory, job.Id);
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

        await response.ShouldBeApiErrorAsync(HttpStatusCode.NotFound, ApiErrorCodes.CompressionJobNotFound);
    }

    private static string JobUrl(Guid jobId)
    {
        return $"/api/compression-jobs/{jobId}";
    }

    private static string DownloadUrl(Guid jobId)
    {
        return $"/api/compression-jobs/{jobId}/download";
    }

    // Mirrors the custom parameters written by SeedJobAsync
    private static CompressionJobResponse ExpectedResponse(
        CompressionJob job,
        string status,
        int? progressPercent,
        string? error = null,
        string? outputFileName = null,
        double? outputDurationSeconds = null,
        long? outputSizeBytes = null
    )
    {
        return new CompressionJobResponse(
            job.Id,
            nameof(OutputContainerFormat.Mp4),
            nameof(VideoCodecOption.H264),
            28,
            640,
            360,
            128,
            nameof(EncodingPresetOption.Medium),
            status,
            progressPercent,
            error,
            outputFileName,
            outputDurationSeconds,
            outputSizeBytes
        );
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

    private async Task<CompressionJob> SeedJobAsync(
        Guid videoId,
        CompressionJobStatus status,
        string? outputFileName = null,
        double? outputDurationSeconds = null,
        long? outputSizeBytes = null,
        string? errorMessage = null,
        byte[]? fileContent = null
    )
    {
        string? resolvedFileName = outputFileName ?? (fileContent is not null ? "holiday_compressed.mp4" : null);
        CompressionJob job = new()
        {
            SourceVideoId = videoId,
            Format = OutputContainerFormat.Mp4,
            VideoCodec = VideoCodecOption.H264,
            Crf = 28,
            TargetWidth = 640,
            TargetHeight = 360,
            AudioBitrateKbps = 128,
            Preset = EncodingPresetOption.Medium,
            Status = status,
            OutputFileName = resolvedFileName,
            OutputDurationSeconds = outputDurationSeconds,
            OutputSizeBytes = outputSizeBytes ?? fileContent?.Length,
            ErrorMessage = errorMessage,
        };

        if (resolvedFileName is not null)
        {
            job.OutputStoragePath = Path.Combine(
                CompressionJob.OutputDirectoryName,
                job.Id.ToString(),
                $"output{Path.GetExtension(resolvedFileName)}"
            );
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
