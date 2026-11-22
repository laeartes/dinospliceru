using System.Net;
using System.Net.Http.Json;
using System.Text;

using DinoSplicer.Api.Data;
using DinoSplicer.Api.Models;
using DinoSplicer.Api.Services;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DinoSplicer.Api.Tests.Controllers;

public class CompressionControllerTests(VideoApiFactory factory) : IClassFixture<VideoApiFactory>
{
    private const int SourceWidth = 1280;
    private const int SourceHeight = 720;

    [Fact]
    public async Task CreateCompression_ReadyVideoWithAllParameters_Returns202CreatesPendingJobAndEnqueuesIt()
    {
        Video video = await SeedVideoAsync(VideoStatus.Ready);
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            CompressUrl(video.Id),
            new
            {
                format = "Webm",
                videoCodec = "Vp9",
                crf = 31,
                targetWidth = 640,
                targetHeight = 360,
                audioBitrateKbps = 96,
                preset = "Slow",
            }
        );

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        CompressionJobResponse? body = await response.Content.ReadFromJsonAsync<CompressionJobResponse>();
        body.Should().NotBeNull();
        body!.JobId.Should().NotBeEmpty();
        body.Status.Should().Be(nameof(CompressionJobStatus.Pending));
        body.Format.Should().Be(nameof(OutputContainerFormat.Webm));
        body.VideoCodec.Should().Be(nameof(VideoCodecOption.Vp9));
        body.Crf.Should().Be(31);
        body.TargetWidth.Should().Be(640);
        body.TargetHeight.Should().Be(360);
        body.AudioBitrateKbps.Should().Be(96);
        body.Preset.Should().Be(nameof(EncodingPresetOption.Slow));

        CompressionJob job = await LoadJobAsync(body.JobId);
        job.SourceVideoId.Should().Be(video.Id);
        job.Format.Should().Be(OutputContainerFormat.Webm);
        job.VideoCodec.Should().Be(VideoCodecOption.Vp9);
        job.Crf.Should().Be(31);
        job.TargetWidth.Should().Be(640);
        job.TargetHeight.Should().Be(360);
        job.AudioBitrateKbps.Should().Be(96);
        job.Preset.Should().Be(EncodingPresetOption.Slow);

        job.Status.Should().Be(CompressionJobStatus.Pending);
        (await WasEnqueuedAsync(body.JobId)).Should().BeTrue("the controller must enqueue the job id after saving it");
    }

    [Theory]
    [InlineData("{}", CompressionRequest.DefaultCrf)]
    [InlineData("""{ "crf": 28 }""", 28)]
    public async Task CreateCompression_OmittedParameters_UseDefaults(string requestBody, int expectedCrf)
    {
        Video video = await SeedVideoAsync(VideoStatus.Ready);
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PostAsync(CompressUrl(video.Id), Json(requestBody));

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        CompressionJobResponse body = (await response.Content.ReadFromJsonAsync<CompressionJobResponse>())!;
        CompressionJob job = await LoadJobAsync(body.JobId);
        job.Format.Should().Be(OutputContainerFormat.Mp4);
        job.VideoCodec.Should().Be(VideoCodecOption.H264);
        job.Crf.Should().Be(expectedCrf);
        job.Preset.Should().Be(EncodingPresetOption.Medium);
        job.TargetWidth.Should().BeNull();
        job.TargetHeight.Should().BeNull();
        job.AudioBitrateKbps.Should().BeNull();
    }

    [Theory]
    [InlineData(VideoStatus.Uploaded)]
    [InlineData(VideoStatus.Processing)]
    [InlineData(VideoStatus.Failed)]
    public async Task CreateCompression_VideoNotReady_Returns409AndCreatesNoJob(VideoStatus status)
    {
        Video video = await SeedVideoAsync(status);
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PostAsync(CompressUrl(video.Id), Json("{}"));

        ApiError error = await response.ShouldBeApiErrorAsync(HttpStatusCode.Conflict, ApiErrorCodes.VideoNotReady);
        error.Error.Should().Contain("must be Ready");
        (await CountJobsAsync(video.Id)).Should().Be(0);
    }

    [Fact]
    public async Task CreateCompression_UnknownVideo_Returns404()
    {
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PostAsync(CompressUrl(Guid.NewGuid()), Json("{}"));

        await response.ShouldBeApiErrorAsync(HttpStatusCode.NotFound, ApiErrorCodes.VideoNotFound);
    }

    [Fact]
    public async Task CreateCompression_MalformedVideoId_Returns404()
    {
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PostAsync("/api/videos/not-a-guid/compress", Json("{}"));

        await response.ShouldBeApiErrorAsync(HttpStatusCode.NotFound, ApiErrorCodes.NotFound);
    }

    [Theory]
    [InlineData("""{ "format": "Avi" }""")]
    [InlineData("""{ "videoCodec": 99 }""")]
    [InlineData("""{ "preset": "Turbo" }""")]
    [InlineData("""{ "crf": 64 }""")]
    [InlineData("""{ "crf": -1 }""")]
    [InlineData("""{ "crf": "abc" }""")]
    [InlineData("""{ "targetWidth": 0, "targetHeight": 720 }""")]
    [InlineData("""{ "audioBitrateKbps": 600 }""")]
    public async Task CreateCompression_InvalidBody_Returns400AndCreatesNoJob(string requestBody)
    {
        Video video = await SeedVideoAsync(VideoStatus.Ready);
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PostAsync(CompressUrl(video.Id), Json(requestBody));

        await response.ShouldBeApiErrorAsync(HttpStatusCode.BadRequest, ApiErrorCodes.ValidationFailed);
        (await CountJobsAsync(video.Id)).Should().Be(0);
    }

    [Theory]
    [InlineData("""{ "format": "Mp4", "videoCodec": "Vp9" }""", ApiErrorCodes.IncompatibleContainerCodec)]
    [InlineData("""{ "format": "Webm", "videoCodec": "H264" }""", ApiErrorCodes.IncompatibleContainerCodec)]
    [InlineData("""{ "videoCodec": "H264", "crf": 55 }""", ApiErrorCodes.InvalidCompressionCrf)]
    [InlineData("""{ "targetWidth": 1281, "targetHeight": 720 }""", ApiErrorCodes.InvalidCompressionResolution)]
    [InlineData("""{ "targetWidth": 640 }""", ApiErrorCodes.InvalidCompressionResolution)]
    [InlineData("""{ "targetWidth": 1920, "targetHeight": 1080 }""", ApiErrorCodes.InvalidCompressionResolution)]
    public async Task CreateCompression_ParametersRejectedByValidator_Returns400WithSpecificCode(
        string requestBody,
        string expectedCode
    )
    {
        Video video = await SeedVideoAsync(VideoStatus.Ready);
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PostAsync(CompressUrl(video.Id), Json(requestBody));
        await response.ShouldBeApiErrorAsync(HttpStatusCode.BadRequest, expectedCode);
        (await CountJobsAsync(video.Id)).Should().Be(0);
    }

    private static string CompressUrl(Guid videoId)
    {
        return $"/api/videos/{videoId}/compress";
    }

    private static StringContent Json(string body)
    {
        return new StringContent(body, Encoding.UTF8, "application/json");
    }

    private async Task<Video> SeedVideoAsync(VideoStatus status)
    {
        Video video = new()
        {
            OriginalFileName = "holiday.mp4",
            ContentType = "video/mp4",
            SizeBytes = 10_485_760,
            DurationSeconds = 120,
            Resolution = new Resolution(SourceWidth, SourceHeight),
            Status = status,
        };
        video.FileName = $"{video.Id}.mp4";

        using IServiceScope scope = factory.Services.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Videos.Add(video);
        await db.SaveChangesAsync();

        return video;
    }

    private async Task<CompressionJob> LoadJobAsync(Guid jobId)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.CompressionJobs.AsNoTracking().SingleAsync(j => j.Id == jobId);
    }

    private async Task<int> CountJobsAsync(Guid videoId)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.CompressionJobs.CountAsync(j => j.SourceVideoId == videoId);
    }

    private async Task<bool> WasEnqueuedAsync(Guid jobId)
    {
        IBackgroundJobQueue<CompressionJob> queue = factory.Services.GetRequiredService<IBackgroundJobQueue<CompressionJob>>();
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(5));

        try
        {
            await foreach (Guid id in queue.DequeueAllAsync(timeout.Token))
            {
                if (id == jobId)
                {
                    return true;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Timed out without seeing the id
        }

        return false;
    }
}
