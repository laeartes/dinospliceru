using System.Net;
using System.Net.Http.Json;

using DinoSplicer.Api.Data;
using DinoSplicer.Api.Models;
using DinoSplicer.Api.Services;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DinoSplicer.Api.Tests.Controllers;

public class SpliceJobsControllerTests(VideoApiFactory factory) : IClassFixture<VideoApiFactory>
{
    [Fact]
    public async Task GetById_FailedJob_Returns200WithStatusAndError()
    {
        Video video = await SeedReadyVideoAsync();
        SpliceJob job = new()
        {
            SourceVideoId = video.Id,
            Mode = SpliceMode.BySize,
            SegmentValue = 2_000_000,
            Status = SpliceJobStatus.Failed,
            ErrorMessage = "The video could not be spliced.",
        };
        await SaveAsync(job);
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(JobUrl(job.Id));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        SpliceJobResponse? body = await response.Content.ReadFromJsonAsync<SpliceJobResponse>();
        body.Should().BeEquivalentTo(
            new SpliceJobResponse(job.Id, nameof(SpliceJobStatus.Failed), "The video could not be spliced.", []));
    }

    [Fact]
    public async Task GetById_ReadyJob_ReturnsOutputsInSegmentIndexOrder()
    {
        Video video = await SeedReadyVideoAsync();
        SpliceJob job = await SeedJobAsync(video.Id, SpliceJobStatus.Ready);
        // Inserted out of order so the response cannot pass by repeating the insert order
        SpliceOutput third = await SeedOutputAsync(job.Id, segmentIndex: 3);
        SpliceOutput first = await SeedOutputAsync(job.Id, segmentIndex: 1);
        SpliceOutput second = await SeedOutputAsync(job.Id, segmentIndex: 2);
        HttpClient client = factory.CreateClient();

        SpliceJobResponse body = await GetJobAsync(client, job.Id);

        body.Outputs.Select(o => o.Id).Should().Equal(first.Id, second.Id, third.Id);
    }

    [Fact]
    public async Task GetById_StatusChangesBetweenPolls_ReturnsCurrentStatusEachTime()
    {
        Video video = await SeedReadyVideoAsync();
        SpliceJob job = await SeedJobAsync(video.Id, SpliceJobStatus.Pending);
        HttpClient client = factory.CreateClient();

        SpliceJobResponse pending = await GetJobAsync(client, job.Id);
        await SetStatusAsync(job.Id, SpliceJobStatus.Processing);
        SpliceJobResponse processing = await GetJobAsync(client, job.Id);
        await SeedOutputAsync(job.Id, segmentIndex: 1);
        await SetStatusAsync(job.Id, SpliceJobStatus.Ready);
        SpliceJobResponse ready = await GetJobAsync(client, job.Id);

        pending.Status.Should().Be(nameof(SpliceJobStatus.Pending));
        processing.Status.Should().Be(nameof(SpliceJobStatus.Processing));
        ready.Status.Should().Be(nameof(SpliceJobStatus.Ready));
        ready.Outputs.Should().ContainSingle();
    }

    [Fact]
    public async Task GetById_MissingId_Returns404()
    {
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(JobUrl(Guid.NewGuid()));

        await response.ShouldBeApiErrorAsync(HttpStatusCode.NotFound, ApiErrorCodes.SpliceJobNotFound);
    }

    [Fact]
    public async Task GetById_AfterSpliceRequest_ReportsReadyOnceTheWorkerIsDone()
    {
        Video video = await SeedReadyVideoAsync(withSampleFile: true);
        HttpClient client = factory.CreateClient();

        HttpResponseMessage createResponse = await client.PostAsJsonAsync(
            $"/api/videos/{video.Id}/splice",
            new { mode = "ByLength", value = 3 });
        createResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);
        SpliceJobResponse created = (await createResponse.Content.ReadFromJsonAsync<SpliceJobResponse>())!;

        SpliceJobResponse finished = await PollUntilFinishedAsync(client, created.JobId);

        finished.Status.Should().Be(nameof(SpliceJobStatus.Ready));
        finished.Error.Should().BeNull();
        finished.Outputs.Should().HaveCount(4);
    }

    private static string JobUrl(Guid jobId)
    {
        return $"/api/splice-jobs/{jobId}";
    }

    private static async Task<SpliceJobResponse> GetJobAsync(HttpClient client, Guid jobId)
    {
        return (await client.GetFromJsonAsync<SpliceJobResponse>(JobUrl(jobId)))!;
    }

    private static async Task<SpliceJobResponse> PollUntilFinishedAsync(HttpClient client, Guid jobId)
    {
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(15));
        while (true)
        {
            SpliceJobResponse job = (await client.GetFromJsonAsync<SpliceJobResponse>(JobUrl(jobId), cts.Token))!;
            if (job.Status is nameof(SpliceJobStatus.Ready) or nameof(SpliceJobStatus.Failed))
            {
                return job;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(200), cts.Token);
        }
    }

    // Inserts the row directly instead of going through the upload, so these tests don't depend on metadata extraction
    private async Task<Video> SeedReadyVideoAsync(bool withSampleFile = false)
    {
        Video video = new()
        {
            OriginalFileName = "holiday.mp4",
            ContentType = "video/mp4",
            SizeBytes = 1024,
            DurationSeconds = SampleVideo.DurationSeconds,
            Status = VideoStatus.Ready,
        };
        video.FileName = $"{video.Id}.mp4";

        if (withSampleFile)
        {
            Directory.CreateDirectory(factory.StorageDirectory);
            File.Copy(await SampleVideo.GetPathAsync(), Path.Combine(factory.StorageDirectory, video.FileName));
        }

        await SaveAsync(video);
        return video;
    }

    private async Task<SpliceJob> SeedJobAsync(Guid videoId, SpliceJobStatus status)
    {
        SpliceJob job = new()
        {
            SourceVideoId = videoId,
            Mode = SpliceMode.ByLength,
            SegmentValue = 3,
            Status = status,
        };
        await SaveAsync(job);
        return job;
    }

    private async Task<SpliceOutput> SeedOutputAsync(Guid jobId, int segmentIndex)
    {
        SpliceOutput output = new()
        {
            SpliceJobId = jobId,
            FileName = $"holiday_part{segmentIndex:D2}.mp4",
            StoragePath = Path.Combine(SpliceProcessingWorker.OutputDirectoryName, jobId.ToString(), $"segment_{segmentIndex:D3}.mp4"),
            SegmentIndex = segmentIndex,
            DurationSeconds = 3,
            SizeBytes = 1024,
        };
        await SaveAsync(output);
        return output;
    }

    private async Task SetStatusAsync(Guid jobId, SpliceJobStatus status)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.SpliceJobs
            .Where(j => j.Id == jobId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(j => j.Status, status));
    }

    private async Task SaveAsync(object entity)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Add(entity);
        await db.SaveChangesAsync();
    }
}
