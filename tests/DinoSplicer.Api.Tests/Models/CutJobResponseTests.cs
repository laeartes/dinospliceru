using System.Text.Json;

using DinoSplicer.Api.Models;

using FluentAssertions;

namespace DinoSplicer.Api.Tests.Models;

public class CutJobResponseTests
{
    [Fact]
    public void FromJob_ReadyJobWithProgress_PopulatesAllFields()
    {
        CutJob job = new()
        {
            Id = Guid.NewGuid(),
            SourceVideoId = Guid.NewGuid(),
            StartSeconds = 2.0,
            EndSeconds = 12.0,
            Mode = CutMode.FrameAccurate,
            Status = CutJobStatus.Ready,
            OutputFileName = "clip_cut.mp4",
            OutputStoragePath = "cuts/job/clip_cut.mp4",
            OutputDurationSeconds = 10.0,
            OutputSizeBytes = 524_288
        };

        CutJobResponse response = CutJobResponse.FromJob(job, progressPercent: 100);

        response.JobId.Should().Be(job.Id);
        response.Mode.Should().Be(nameof(CutMode.FrameAccurate));
        response.StartSeconds.Should().Be(2.0);
        response.EndSeconds.Should().Be(12.0);
        response.Status.Should().Be(nameof(CutJobStatus.Ready));
        response.ProgressPercent.Should().Be(100);
        response.Error.Should().BeNull();
        response.OutputFileName.Should().Be("clip_cut.mp4");
        response.OutputDurationSeconds.Should().Be(10.0);
        response.OutputSizeBytes.Should().Be(524_288);
    }

    [Fact]
    public void FromJob_PendingJobWithoutProgress_PreservesNullValues()
    {
        CutJob job = new()
        {
            Id = Guid.NewGuid(),
            SourceVideoId = Guid.NewGuid(),
            StartSeconds = 0.0,
            EndSeconds = 5.0,
            Mode = CutMode.FastCopy,
            Status = CutJobStatus.Pending
        };

        CutJobResponse response = CutJobResponse.FromJob(job);

        response.Status.Should().Be(nameof(CutJobStatus.Pending));
        response.ProgressPercent.Should().BeNull();
        response.Error.Should().BeNull();
        response.OutputFileName.Should().BeNull();
        response.OutputDurationSeconds.Should().BeNull();
        response.OutputSizeBytes.Should().BeNull();
    }

    [Fact]
    public void CutJobResponse_JsonSerialization_IncludesProgressPercentAndOutputMetadata()
    {
        CutJobResponse response = new(
            Guid.NewGuid(),
            "FastCopy",
            1.0,
            6.0,
            "Processing",
            42,
            null,
            null,
            null,
            null
        );

        string json = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        json.Should().Contain("\"progressPercent\":42");
        json.Should().Contain("\"mode\":\"FastCopy\"");
        json.Should().Contain("\"status\":\"Processing\"");
    }
}
