using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using DinoSplicer.Api.Models;

using FluentAssertions;

using Microsoft.AspNetCore.Mvc.Testing;

namespace DinoSplicer.Api.Tests.Controllers;

public class ConfigControllerTests(VideoApiFactory factory) : IClassFixture<VideoApiFactory>
{
    private const string ConfigUrl = "/api/config";

    [Fact]
    public async Task Get_Default_Returns200WithConfiguredUploadLimits()
    {
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(ConfigUrl);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        ConfigResponse? body = await response.Content.ReadFromJsonAsync<ConfigResponse>();
        body.Should().NotBeNull();
        body!.VideoUpload.MaxFileSizeBytes.Should().Be(VideoApiFactory.TestMaxFileSizeBytes);
        body.VideoUpload.AllowedExtensions.Should().Equal(".mp4", ".mov", ".mkv", ".webm");
        body.VideoUpload.AllowedContentTypes.Should().Equal("video/mp4", "video/quicktime", "video/x-matroska", "video/matroska", "video/webm");
    }

    [Fact]
    public async Task Get_Default_ReturnsNestedCamelCaseShape()
    {
        HttpClient client = factory.CreateClient();

        using JsonDocument json = JsonDocument.Parse(await client.GetStringAsync(ConfigUrl));

        JsonElement videoUpload = json.RootElement.GetProperty("videoUpload");
        videoUpload.GetProperty("maxFileSizeBytes").GetInt64().Should().Be(VideoApiFactory.TestMaxFileSizeBytes);
        videoUpload.GetProperty("allowedExtensions").ValueKind.Should().Be(JsonValueKind.Array);
        videoUpload.GetProperty("allowedContentTypes").ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact]
    public async Task Get_Default_DoesNotExposeStoragePath()
    {
        HttpClient client = factory.CreateClient();

        string body = await client.GetStringAsync(ConfigUrl);

        body.Should().NotContainEquivalentOf("storagePath");
        body.Should().NotContain(factory.StorageDirectory);
    }

    [Fact]
    public async Task Get_LimitsChangedInConfig_ReturnsChangedLimits()
    {
        await using WebApplicationFactory<Program> customFactory = factory.WithConfiguration(new Dictionary<string, string?>
        {
            ["VideoUpload:MaxFileSizeBytes"] = "2048",
            ["VideoUpload:AllowedExtensions:4"] = ".avi",
        });
        HttpClient client = customFactory.CreateClient();

        ConfigResponse? body = await client.GetFromJsonAsync<ConfigResponse>(ConfigUrl);

        body!.VideoUpload.MaxFileSizeBytes.Should().Be(2048);
        body.VideoUpload.AllowedExtensions.Should().Contain(".avi");
    }
}
