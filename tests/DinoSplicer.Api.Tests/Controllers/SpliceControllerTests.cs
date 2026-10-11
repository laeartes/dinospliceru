using System.Net;
using System.Net.Http.Json;
using System.Text;

using DinoSplicer.Api.Data;
using DinoSplicer.Api.Models;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DinoSplicer.Api.Tests.Controllers;

public class SpliceControllerTests(VideoApiFactory factory) : IClassFixture<VideoApiFactory>
{
    // Seeded videos are 120 seconds long and 10 MiB; the out-of-range cases below are chosen around that
    private const int SeededSizeBytes = 10_485_760;
    private const double SeededDurationSeconds = 120;

    [Theory]
    [InlineData("ByLength", 30.0, SpliceMode.ByLength)]
    [InlineData("BySize", 2_097_152.0, SpliceMode.BySize)]
    public async Task CreateSplice_ReadyVideoWithValidRequest_Returns202AndCreatesPendingJob(
        string mode,
        double value,
        SpliceMode expectedMode)
    {
        Video video = await SeedVideoAsync(VideoStatus.Ready);
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(SpliceUrl(video.Id), new { mode, value });

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        SpliceJobResponse? body = await response.Content.ReadFromJsonAsync<SpliceJobResponse>();
        body.Should().NotBeNull();
        body!.JobId.Should().NotBeEmpty();
        body.Status.Should().Be(nameof(SpliceJobStatus.Pending));

        using IServiceScope scope = factory.Services.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        SpliceJob job = await db.SpliceJobs.SingleAsync(j => j.Id == body.JobId);
        job.SourceVideoId.Should().Be(video.Id);
        job.Mode.Should().Be(expectedMode);
        job.SegmentValue.Should().Be(value);
        job.Status.Should().Be(SpliceJobStatus.Pending);
    }

    [Theory]
    [InlineData(VideoStatus.Uploaded)]
    [InlineData(VideoStatus.Processing)]
    [InlineData(VideoStatus.Failed)]
    public async Task CreateSplice_VideoNotReady_Returns409AndCreatesNoJob(VideoStatus status)
    {
        Video video = await SeedVideoAsync(status);
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            SpliceUrl(video.Id),
            new { mode = "ByLength", value = 30 });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.Content.ReadAsStringAsync()).Should().Contain("must be Ready");
        (await CountJobsAsync(video.Id)).Should().Be(0);
    }

    [Fact]
    public async Task CreateSplice_UnknownVideo_Returns404()
    {
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            SpliceUrl(Guid.NewGuid()),
            new { mode = "ByLength", value = 30 });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Video not found");
    }

    [Fact]
    public async Task CreateSplice_MalformedVideoId_Returns404()
    {
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/videos/not-a-guid/splice",
            new { mode = "ByLength", value = 30 });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("""{ "mode": "Sideways", "value": 30 }""")]
    [InlineData("""{ "mode": 99, "value": 30 }""")]
    [InlineData("""{ "value": 30 }""")]
    [InlineData("""{ "mode": "ByLength" }""")]
    [InlineData("""{ "mode": "ByLength", "value": "abc" }""")]
    [InlineData("{}")]
    public async Task CreateSplice_InvalidBody_Returns400AndCreatesNoJob(string body)
    {
        Video video = await SeedVideoAsync(VideoStatus.Ready);
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PostAsync(SpliceUrl(video.Id), Json(body));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await CountJobsAsync(video.Id)).Should().Be(0);
    }

    [Theory]
    [InlineData("ByLength", -5.0)]
    [InlineData("ByLength", 0.0)]
    [InlineData("ByLength", 0.5)]
    [InlineData("ByLength", 120.5)]
    [InlineData("BySize", -1.0)]
    [InlineData("BySize", 0.0)]
    [InlineData("BySize", 1_000.0)]
    [InlineData("BySize", 20_000_000.0)]
    public async Task CreateSplice_ValueOutOfRange_Returns400AndCreatesNoJob(string mode, double value)
    {
        Video video = await SeedVideoAsync(VideoStatus.Ready);
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(SpliceUrl(video.Id), new { mode, value });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await CountJobsAsync(video.Id)).Should().Be(0);
    }

    private static string SpliceUrl(Guid videoId)
    {
        return $"/api/videos/{videoId}/splice";
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
        return await db.SpliceJobs.CountAsync(j => j.SourceVideoId == videoId);
    }
}
