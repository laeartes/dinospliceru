using System.Text.Json;

using DinoSplicer.Api.Models;

using FluentAssertions;

namespace DinoSplicer.Api.Tests.Models;

public class VideoResponseTests
{
    [Fact]
    public void FromVideo_PopulatesAllFieldsIncludingMetadata()
    {
        Video video = new()
        {
            Id = Guid.NewGuid(),
            OriginalFileName = "movie.mp4",
            ContentType = "video/mp4",
            SizeBytes = 1048576,
            DurationSeconds = 42.5,
            Resolution = new Resolution(1920, 1080),
            Codec = "h264",
            Status = VideoStatus.Ready,
            CreatedAt = DateTime.UtcNow
        };

        VideoResponse response = VideoResponse.FromVideo(video);

        response.Id.Should().Be(video.Id);
        response.FileName.Should().Be(video.OriginalFileName);
        response.ContentType.Should().Be(video.ContentType);
        response.SizeBytes.Should().Be(video.SizeBytes);
        response.DurationSeconds.Should().Be(video.DurationSeconds);
        response.Resolution.Should().Be(new Resolution(1920, 1080));
        response.Codec.Should().Be("h264");
        response.Status.Should().Be(nameof(VideoStatus.Ready));
        response.CreatedAt.Should().Be(video.CreatedAt);
    }

    [Fact]
    public void FromVideo_NullMetadata_PreservesNullValues()
    {
        Video video = new()
        {
            Id = Guid.NewGuid(),
            OriginalFileName = "raw.mp4",
            ContentType = "video/mp4",
            SizeBytes = 500,
            Status = VideoStatus.Uploaded
        };

        VideoResponse response = VideoResponse.FromVideo(video);

        response.Resolution.Should().BeNull();
        response.Codec.Should().BeNull();
        response.DurationSeconds.Should().BeNull();
        response.Status.Should().Be(nameof(VideoStatus.Uploaded));
    }

    [Fact]
    public void VideoResponse_JsonSerialization_IncludesNestedResolutionAndCodec()
    {
        VideoResponse response = new(
            Guid.NewGuid(),
            "clip.mp4",
            "video/mp4",
            1024,
            12.5,
            new Resolution(1280, 720),
            "vp9",
            "Ready",
            DateTime.UtcNow
        );

        string json = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        json.Should().Contain("\"resolution\":{\"width\":1280,\"height\":720}");
        json.Should().Contain("\"codec\":\"vp9\"");
    }
}
