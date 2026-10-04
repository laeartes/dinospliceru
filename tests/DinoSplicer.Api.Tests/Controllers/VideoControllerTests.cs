using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

using DinoSplicer.Api.Data;
using DinoSplicer.Api.Models;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DinoSplicer.Api.Tests.Controllers;

public class VideoControllerTests(VideoApiFactory factory) : IClassFixture<VideoApiFactory>
{
    private const string UploadUrl = "/api/videos";
    private const int SeededFileSizeBytes = 1024;

    [Fact]
    public async Task Upload_ValidFIle_Returns201WithVideoRecord()
    {
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PostAsync(UploadUrl, CreateUpload(("clip.mp4", "video/mp4", 1024)));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        VideoUploadResponse? body = await response.Content.ReadFromJsonAsync<VideoUploadResponse>();
        body.Should().NotBeNull();
        body!.Id.Should().NotBeEmpty();
        body.FileName.Should().Be("clip.mp4");
        body.SizeBytes.Should().Be(1024);
        body.Status.Should().Be(nameof(VideoStatus.Uploaded));
        response.Headers.Location!.ToString().Should().EndWith(body.Id.ToString());
    }

    [Fact]
    public async Task Upload_FileOverMaxSize_Returns413()
    {
        HttpClient client = factory.CreateClient();
        int oversized = (int)VideoApiFactory.TestMaxFileSizeBytes + 1;

        HttpResponseMessage response = await client.PostAsync(UploadUrl, CreateUpload(("big.mp4", "video/mp4", oversized)));

        response.StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);
        (await response.Content.ReadAsStringAsync()).Should().Contain("maximum allowed size");
    }

    [Theory]
    [InlineData("notes.txt", "text/plain")]
    [InlineData("clip.avi", "video/x-msvideo")]
    [InlineData("clip.mp4", "text/plain")]
    public async Task Upload_InvalidExtensionOrContentType_Returns415(string fileName, string contentType)
    {
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PostAsync(UploadUrl, CreateUpload((fileName, contentType, 1024)));

        response.StatusCode.Should().Be(HttpStatusCode.UnsupportedMediaType);
    }

    [Fact]
    public async Task Upload_MultipleFiles_Returns400()
    {
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PostAsync(
            UploadUrl,
            CreateUpload(("a.mp4", "video/mp4", 1024), ("b.mp4", "video/mp4", 1024)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Exactly one file");
    }

    [Fact]
    public async Task Upload_NoFile_Returns400()
    {
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PostAsync(UploadUrl, CreateUpload());

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Upload_ValidFile_CreatesMatchingDbRowAndFileOnDisk()
    {
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PostAsync(UploadUrl, CreateUpload(("holiday.webm", "video/webm", 2048)));
        VideoUploadResponse body = (await response.Content.ReadFromJsonAsync<VideoUploadResponse>())!;

        using IServiceScope scope = factory.Services.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Video? video = await db.Videos.SingleOrDefaultAsync(v => v.Id == body.Id);

        video.Should().NotBeNull();
        video!.OriginalFileName.Should().Be("holiday.webm");
        video.FileName.Should().Be($"{body.Id}.webm");
        video.ContentType.Should().Be("video/webm");
        video.SizeBytes.Should().Be(2048);
        video.Status.Should().BeOneOf(VideoStatus.Uploaded, VideoStatus.Processing, VideoStatus.Failed);

        string fullPath = Path.Combine(factory.StorageDirectory, video.FileName);
        File.Exists(fullPath).Should().BeTrue();
        new FileInfo(fullPath).Length.Should().Be(2048);
    }

    [Fact]
    public async Task GetById_ExistingId_Returns200WithVideo()
    {
        Video video = await SeedVideoAsync();
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(VideoUrl(video.Id));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        VideoResponse? body = await response.Content.ReadFromJsonAsync<VideoResponse>();
        body.Should().NotBeNull();
        body!.Id.Should().Be(video.Id);
        body.FileName.Should().Be(video.OriginalFileName);
        body.ContentType.Should().Be(video.ContentType);
        body.SizeBytes.Should().Be(video.SizeBytes);
        body.DurationSeconds.Should().Be(video.DurationSeconds);
        body.Resolution.Should().BeNull();
        body.Codec.Should().BeNull();
        body.Status.Should().Be(nameof(VideoStatus.Uploaded));
        // Postgres stores microseconds, DateTime has 100ns ticks, so an exact match can fail
        body.CreatedAt.Should().BeCloseTo(video.CreatedAt, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task GetById_MissingId_Returns404()
    {
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(VideoUrl(Guid.NewGuid()));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetById_MalformedId_Returns404()
    {
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/api/videos/not-a-guid");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_ExistingId_Returns204AndRemovesRowAndFile()
    {
        Video video = await SeedVideoAsync();
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.DeleteAsync(VideoUrl(video.Id));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await VideoExistsInDbAsync(video.Id)).Should().BeFalse();
        File.Exists(GetFilePath(video)).Should().BeFalse();
    }

    [Fact]
    public async Task Delete_MissingId_Returns404()
    {
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.DeleteAsync(VideoUrl(Guid.NewGuid()));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_FileAlreadyMissingFromDisk_Returns204AndRemovesRow()
    {
        Video video = await SeedVideoAsync(createFile: false);
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.DeleteAsync(VideoUrl(video.Id));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await VideoExistsInDbAsync(video.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task Delete_VideoIsProcessing_Returns409AndKeepsRowAndFile()
    {
        Video video = await SeedVideoAsync(VideoStatus.Processing);
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.DeleteAsync(VideoUrl(video.Id));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await VideoExistsInDbAsync(video.Id)).Should().BeTrue();
        File.Exists(GetFilePath(video)).Should().BeTrue();
    }

    private static MultipartFormDataContent CreateUpload(params (string FileName, string ContentType, int SizeBytes)[] files)
    {
        MultipartFormDataContent content = new();
        foreach ((string fileName, string contentType, int sizeBytes) in files)
        {
            ByteArrayContent fileContent = new(new byte[sizeBytes]);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            content.Add(fileContent, "file", fileName);
        }

        return content;
    }

    private static string VideoUrl(Guid id)
    {
        return $"/api/videos/{id}";
    }

    private string GetFilePath(Video video)
    {
        return Path.Combine(factory.StorageDirectory, video.FileName);
    }

    // Inserts the row directly instead of going through POST, so these tests don't depend on upload
    private async Task<Video> SeedVideoAsync(VideoStatus status = VideoStatus.Uploaded, bool createFile = true)
    {
        Video video = new()
        {
            OriginalFileName = "holiday.mp4",
            ContentType = "video/mp4",
            SizeBytes = SeededFileSizeBytes,
            DurationSeconds = 12.5,
            Status = status,
        };
        video.FileName = $"{video.Id}.mp4";

        if (createFile)
        {
            Directory.CreateDirectory(factory.StorageDirectory);
            await File.WriteAllBytesAsync(GetFilePath(video), new byte[SeededFileSizeBytes]);
        }

        using IServiceScope scope = factory.Services.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Videos.Add(video);
        await db.SaveChangesAsync();

        return video;
    }

    private async Task<bool> VideoExistsInDbAsync(Guid id)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Videos.AnyAsync(v => v.Id == id);
    }
}
