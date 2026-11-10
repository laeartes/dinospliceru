using System.Text.Json;

using DinoSplicer.Api.Models;

using FluentAssertions;

namespace DinoSplicer.Api.Tests.Models;

public class CompressionJobResponseTests
{
    [Fact]
    public void FromJob_ReadyJobWithProgress_PopulatesAllFields()
    {
        CompressionJob job = new()
        {
            SourceVideoId = Guid.NewGuid(),
            Format = OutputContainerFormat.Webm,
            VideoCodec = VideoCodecOption.Vp9,
            Crf = 31,
            TargetWidth = 1280,
            TargetHeight = 720,
            AudioBitrateKbps = 96,
            Preset = EncodingPresetOption.Slow,
            Status = CompressionJobStatus.Ready,
            OutputFileName = "holiday_compressed.webm",
            OutputDurationSeconds = 42.5,
            OutputSizeBytes = 1_048_576
        };

        CompressionJobResponse response = CompressionJobResponse.FromJob(job, 100);

        response.Should().Be(new CompressionJobResponse(
            job.Id, "Webm", "Vp9", 31, 1280, 720, 96, "Slow", "Ready", 100, null,
            "holiday_compressed.webm", 42.5, 1_048_576));
    }

    [Fact]
    public void FromJob_PendingJobWithoutProgress_PreservesNullValues()
    {
        CompressionJob job = new()
        {
            SourceVideoId = Guid.NewGuid(),
            Format = OutputContainerFormat.Mp4,
            VideoCodec = VideoCodecOption.H264,
            Crf = 23,
            Preset = EncodingPresetOption.Medium
        };

        CompressionJobResponse response = CompressionJobResponse.FromJob(job);

        response.Status.Should().Be("Pending");
        response.ProgressPercent.Should().BeNull();
        response.TargetWidth.Should().BeNull();
        response.TargetHeight.Should().BeNull();
        response.AudioBitrateKbps.Should().BeNull();
        response.OutputFileName.Should().BeNull();
        response.OutputSizeBytes.Should().BeNull();
    }

    [Fact]
    public void CompressionJobResponse_JsonSerialization_UsesCamelCaseAndStringEnums()
    {
        CompressionJobResponse response = new(
            Guid.NewGuid(), "Mp4", "H264", 23, null, null, null, "Medium", "Processing", 42, null, null, null, null);

        string json = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        json.Should().Contain("\"progressPercent\":42");
        json.Should().Contain("\"videoCodec\":\"H264\"");
        json.Should().Contain("\"status\":\"Processing\"");
    }
}