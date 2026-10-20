using System.Net;
using System.Net.Http.Headers;
using System.Text;

using DinoSplicer.Api.Data;
using DinoSplicer.Api.Models;
using DinoSplicer.Api.Services;

using FluentAssertions;

using Microsoft.Extensions.DependencyInjection;

namespace DinoSplicer.Api.Tests.Controllers;

public class SpliceOutputsControllerTests(VideoApiFactory factory) : IClassFixture<VideoApiFactory>
{
    [Fact]
    public async Task Download_ReadyOutput_ReturnsFileWithAttachmentHeaders()
    {
        byte[] content = Encoding.UTF8.GetBytes("segment bytes");
        SpliceOutput output = await SeedOutputAsync(SpliceJobStatus.Ready, content);
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(DownloadUrl(output.Id));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("video/mp4");
        response.Content.Headers.ContentLength.Should().Be(content.Length);
        ContentDispositionHeaderValue disposition = response.Content.Headers.ContentDisposition!;
        disposition.DispositionType.Should().Be("attachment");
        disposition.FileName!.Trim('"').Should().Be("holiday_part01.mp4");
        (await response.Content.ReadAsByteArrayAsync()).Should().Equal(content);
    }

    [Fact]
    public async Task Download_OutputWithNonAsciiFileName_EncodesNameInDisposition()
    {
        const string fileName = "släpp vidéo_part01.mp4";
        SpliceOutput output = await SeedOutputAsync(SpliceJobStatus.Ready, [1, 2, 3], fileName);
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(DownloadUrl(output.Id));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentDisposition!.FileNameStar.Should().Be(fileName);
    }

    [Theory]
    [InlineData(SpliceJobStatus.Pending)]
    [InlineData(SpliceJobStatus.Processing)]
    [InlineData(SpliceJobStatus.Failed)]
    public async Task Download_JobNotReady_Returns409AndNoFile(SpliceJobStatus status)
    {
        // The file exists on disk, so only the status check can stop the download
        SpliceOutput output = await SeedOutputAsync(status, Encoding.UTF8.GetBytes("segment bytes"));
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(DownloadUrl(output.Id));

        await response.ShouldBeApiErrorAsync(HttpStatusCode.Conflict, ApiErrorCodes.SpliceJobNotReady);
        response.Content.Headers.ContentDisposition.Should().BeNull();
    }

    [Fact]
    public async Task Download_MissingId_Returns404()
    {
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(DownloadUrl(Guid.NewGuid()));

        await response.ShouldBeApiErrorAsync(HttpStatusCode.NotFound, ApiErrorCodes.SpliceOutputNotFound);
    }

    [Fact]
    public async Task Download_ReadyOutputWithoutFileOnDisk_Returns404()
    {
        SpliceOutput output = await SeedOutputAsync(SpliceJobStatus.Ready, fileContent: null);
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(DownloadUrl(output.Id));

        await response.ShouldBeApiErrorAsync(HttpStatusCode.NotFound, ApiErrorCodes.SpliceOutputFileMissing);
    }

    private static string DownloadUrl(Guid outputId)
    {
        return $"/api/splice-outputs/{outputId}/download";
    }

    // Inserts the rows directly; a null fileContent leaves the segment file missing from storage
    private async Task<SpliceOutput> SeedOutputAsync(
        SpliceJobStatus jobStatus,
        byte[]? fileContent,
        string fileName = "holiday_part01.mp4"
    )
    {
        Video video = new()
        {
            OriginalFileName = "holiday.mp4",
            ContentType = "video/mp4",
            SizeBytes = 1024,
            Status = VideoStatus.Ready,
        };
        video.FileName = $"{video.Id}.mp4";

        SpliceJob job = new()
        {
            SourceVideoId = video.Id,
            Mode = SpliceMode.ByLength,
            SegmentValue = 3,
            Status = jobStatus,
        };

        SpliceOutput output = new()
        {
            SpliceJobId = job.Id,
            FileName = fileName,
            StoragePath = Path.Combine(SpliceProcessingWorker.OutputDirectoryName, job.Id.ToString(), "segment_001.mp4"),
            SegmentIndex = 1,
            DurationSeconds = 3,
            SizeBytes = fileContent?.Length ?? 1024,
        };

        if (fileContent is not null)
        {
            string filePath = Path.Combine(factory.StorageDirectory, output.StoragePath);
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            await File.WriteAllBytesAsync(filePath, fileContent);
        }

        using IServiceScope scope = factory.Services.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.AddRange(video, job, output);
        await db.SaveChangesAsync();
        return output;
    }
}
