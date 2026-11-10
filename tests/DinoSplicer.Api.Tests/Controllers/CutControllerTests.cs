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

public class CutControllerTests(VideoApiFactory factory) : IClassFixture<VideoApiFactory>
{
    // Seeded videos are 120 seconds long; the out-of-range cases below are chosen around that
    private const int SeededSizeBytes = 10_485_760;
    private const double SeededDurationSeconds = 120;

    [Theory]
    [InlineData("FastCopy", 0.0, 15.0, CutMode.FastCopy)]
    [InlineData("FrameAccurate", 2.5, 10.0, CutMode.FrameAccurate)]
    [InlineData("FastCopy", 0.0, 120.0, CutMode.FastCopy)]
    public async Task CreateCut_ReadyVideoWithValidRequest_Returns202CreatesPendingJobAndEnqueuesIt(
        string mode,
        double startSeconds,
        double endSeconds,
        CutMode expectedMode)
    {
        Video video = await SeedVideoAsync(VideoStatus.Ready);
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            CutUrl(video.Id),
            new { mode, startSeconds, endSeconds });

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        CutJobResponse? body = await response.Content.ReadFromJsonAsync<CutJobResponse>();
        body.Should().NotBeNull();
        body!.JobId.Should().NotBeEmpty();
        body.Status.Should().Be(nameof(CutJobStatus.Pending));
        body.Mode.Should().Be(expectedMode.ToString());
        body.StartSeconds.Should().Be(startSeconds);
        body.EndSeconds.Should().Be(endSeconds);

        using IServiceScope scope = factory.Services.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        CutJob job = await db.CutJobs.SingleAsync(j => j.Id == body.JobId);
        job.SourceVideoId.Should().Be(video.Id);
        job.Mode.Should().Be(expectedMode);
        job.StartSeconds.Should().Be(startSeconds);
        job.EndSeconds.Should().Be(endSeconds);

        // Nothing consumes IBackgroundJobQueue<CutJob> yet, so the job is still Pending.
        // Once a cut worker exists, relax this like SpliceControllerTests does.
        job.Status.Should().Be(CutJobStatus.Pending);

        (await WasEnqueuedAsync(body.JobId)).Should().BeTrue("the controller must enqueue the job id after saving it");
    }

    [Theory]
    [InlineData(VideoStatus.Uploaded)]
    [InlineData(VideoStatus.Processing)]
    [InlineData(VideoStatus.Failed)]
    public async Task CreateCut_VideoNotReady_Returns409AndCreatesNoJob(VideoStatus status)
    {
        Video video = await SeedVideoAsync(status);
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            CutUrl(video.Id),
            new { mode = "FastCopy", startSeconds = 0, endSeconds = 10 });

        ApiError error = await response.ShouldBeApiErrorAsync(HttpStatusCode.Conflict, ApiErrorCodes.VideoNotReady);
        error.Error.Should().Contain("must be Ready");
        (await CountJobsAsync(video.Id)).Should().Be(0);
    }

    [Fact]
    public async Task CreateCut_UnknownVideo_Returns404()
    {
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            CutUrl(Guid.NewGuid()),
            new { mode = "FastCopy", startSeconds = 0, endSeconds = 10 });

        await response.ShouldBeApiErrorAsync(HttpStatusCode.NotFound, ApiErrorCodes.VideoNotFound);
    }

    [Fact]
    public async Task CreateCut_MalformedVideoId_Returns404()
    {
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/videos/not-a-guid/cut",
            new { mode = "FastCopy", startSeconds = 0, endSeconds = 10 });

        await response.ShouldBeApiErrorAsync(HttpStatusCode.NotFound, ApiErrorCodes.NotFound);
    }

    [Theory]
    [InlineData("""{ "startSeconds": 0, "endSeconds": 10, "mode": "Sideways" }""")]
    [InlineData("""{ "startSeconds": 0, "endSeconds": 10, "mode": 99 }""")]
    [InlineData("""{ "startSeconds": 0, "endSeconds": 10 }""")]
    [InlineData("""{ "startSeconds": "abc", "endSeconds": 10, "mode": "FastCopy" }""")]
    [InlineData("""{ "startSeconds": 0, "endSeconds": "abc", "mode": "FastCopy" }""")]
    [InlineData("{}")]
    public async Task CreateCut_InvalidBody_Returns400AndCreatesNoJob(string body)
    {
        Video video = await SeedVideoAsync(VideoStatus.Ready);
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PostAsync(CutUrl(video.Id), Json(body));

        await response.ShouldBeApiErrorAsync(HttpStatusCode.BadRequest, ApiErrorCodes.ValidationFailed);
        (await CountJobsAsync(video.Id)).Should().Be(0);
    }

    // The request's own data annotations may reject these before the validator runs, so either code is fine
    [Theory]
    [InlineData(-1.0, 10.0)]
    [InlineData(-0.1, 10.0)]
    [InlineData(5.0, 5.0)]
    [InlineData(8.0, 3.0)]
    public async Task CreateCut_InvalidTimestampBounds_Returns400AndCreatesNoJob(double startSeconds, double endSeconds)
    {
        Video video = await SeedVideoAsync(VideoStatus.Ready);
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            CutUrl(video.Id),
            new { mode = "FastCopy", startSeconds, endSeconds });

        ApiError error = await response.ShouldBeApiErrorAsync(HttpStatusCode.BadRequest);
        error.Code.Should().BeOneOf(ApiErrorCodes.ValidationFailed, ApiErrorCodes.InvalidCutRange);
        (await CountJobsAsync(video.Id)).Should().Be(0);
    }

    // These depend on the video, so only CutRequestValidator can reject them
    [Theory]
    [InlineData(0.0, 120.5)]
    [InlineData(100.0, 200.0)]
    [InlineData(130.0, 140.0)]
    [InlineData(0.0, 0.05)]
    [InlineData(10.0, 10.05)]
    public async Task CreateCut_RangeRejectedByValidator_Returns400WithInvalidCutRange(double startSeconds, double endSeconds)
    {
        Video video = await SeedVideoAsync(VideoStatus.Ready);
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            CutUrl(video.Id),
            new { mode = "FastCopy", startSeconds, endSeconds });

        await response.ShouldBeApiErrorAsync(HttpStatusCode.BadRequest, ApiErrorCodes.InvalidCutRange);
        (await CountJobsAsync(video.Id)).Should().Be(0);
    }

    // JSON has no NaN/Infinity literals, so the only way to send them is as strings
    [Theory]
    [InlineData("""{ "startSeconds": "NaN", "endSeconds": 10, "mode": "FastCopy" }""")]
    [InlineData("""{ "startSeconds": 0, "endSeconds": "NaN", "mode": "FastCopy" }""")]
    [InlineData("""{ "startSeconds": 0, "endSeconds": "Infinity", "mode": "FastCopy" }""")]
    [InlineData("""{ "startSeconds": "-Infinity", "endSeconds": 10, "mode": "FastCopy" }""")]
    public async Task CreateCut_NonFiniteTimestamp_Returns400AndCreatesNoJob(string body)
    {
        Video video = await SeedVideoAsync(VideoStatus.Ready);
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PostAsync(CutUrl(video.Id), Json(body));

        ApiError error = await response.ShouldBeApiErrorAsync(HttpStatusCode.BadRequest);
        error.Code.Should().BeOneOf(ApiErrorCodes.ValidationFailed, ApiErrorCodes.InvalidCutRange);
        (await CountJobsAsync(video.Id)).Should().Be(0);
    }

    private static string CutUrl(Guid videoId)
    {
        return $"/api/videos/{videoId}/cut";
    }

    private static StringContent Json(string body)
    {
        return new StringContent(body, Encoding.UTF8, "application/json");
    }

    // Inserts the row directly instead of going through the upload, so these tests don't depend on the worker
    private async Task<Video> SeedVideoAsync(VideoStatus status)
    {
        Video video = new()
        {
            OriginalFileName = "holiday.mp4",
            ContentType = "video/mp4",
            SizeBytes = SeededSizeBytes,
            DurationSeconds = SeededDurationSeconds,
            Status = status,
        };
        video.FileName = $"{video.Id}.mp4";

        using IServiceScope scope = factory.Services.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Videos.Add(video);
        await db.SaveChangesAsync();

        return video;
    }

    private async Task<int> CountJobsAsync(Guid videoId)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.CutJobs.CountAsync(j => j.SourceVideoId == videoId);
    }

    // The queue is a singleton shared by every test in this class, so ids from earlier tests are
    // still in the channel; read past them until this job's id turns up or the timeout expires.
    private async Task<bool> WasEnqueuedAsync(Guid jobId)
    {
        IBackgroundJobQueue<CutJob> queue = factory.Services.GetRequiredService<IBackgroundJobQueue<CutJob>>();
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
