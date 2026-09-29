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
        video.Status.Should().Be(VideoStatus.Uploaded);
        video.StoragePath.Should().Be($"{body.Id}.webm");
        Path.IsPathRooted(video.StoragePath).Should().BeFalse();

        string fullPath = Path.Combine(factory.StorageDirectory, video.StoragePath);
        File.Exists(fullPath).Should().BeTrue();
        new FileInfo(fullPath).Length.Should().Be(2048);
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
}
