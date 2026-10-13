using DinoSplicer.Api.Models;
using DinoSplicer.Api.Services;

using FluentAssertions;

namespace DinoSplicer.Api.Tests.Services;

public class VideoSplicerTests : IDisposable
{
    private readonly string _outputDirectory = Path.Combine(Path.GetTempPath(), "dinosplicer-tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_outputDirectory))
        {
            Directory.Delete(_outputDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task Splice_ByLength_ProducesSegmentsOfRequestedLength()
    {
        string sourcePath = await SampleVideo.GetPathAsync();

        IReadOnlyList<SpliceSegment> segments = await VideoSplicer.Splice(sourcePath, _outputDirectory, SpliceMode.ByLength, 3);

        // 10 seconds in 3 second pieces: three full segments and a 1 second remainder
        segments.Select(s => s.Index).Should().Equal(1, 2, 3, 4);
        segments.Take(3).Should().AllSatisfy(s => s.DurationSeconds.Should().BeApproximately(3, 0.1));
        segments.Sum(s => s.DurationSeconds).Should().BeApproximately(SampleVideo.DurationSeconds, 0.2);
        segments.Should().AllSatisfy(s => new FileInfo(s.FilePath).Length.Should().Be(s.SizeBytes));
    }

    [Fact]
    public async Task Splice_BySize_KeepsEverySegmentUnderTheLimit()
    {
        string sourcePath = await SampleVideo.GetPathAsync();
        long limit = new FileInfo(sourcePath).Length / 3;

        IReadOnlyList<SpliceSegment> segments = await VideoSplicer.Splice(sourcePath, _outputDirectory, SpliceMode.BySize, limit);

        segments.Should().HaveCountGreaterThanOrEqualTo(3);
        segments.Should().AllSatisfy(s => s.SizeBytes.Should().BeLessThanOrEqualTo(limit));
        segments.Sum(s => s.DurationSeconds).Should().BeApproximately(SampleVideo.DurationSeconds, 0.2);
    }

    [Fact]
    public async Task Splice_OnlyRequiredArguments_DoesNotOverwriteExistingOutput()
    {
        string sourcePath = await SampleVideo.GetPathAsync();
        Directory.CreateDirectory(_outputDirectory);
        string existingFile = Path.Combine(_outputDirectory, "segment_000.mp4");
        await File.WriteAllTextAsync(existingFile, "earlier output");

        Func<Task> splice = () => VideoSplicer.Splice(sourcePath, _outputDirectory, SpliceMode.ByLength, 3);

        // overwriteExisting defaults to false
        await splice.Should().ThrowAsync<IOException>();
        (await File.ReadAllTextAsync(existingFile)).Should().Be("earlier output");
    }

    [Fact]
    public void PlanCutTimes_TwoStretchesFitPerSegment_CutsAtEveryOtherKeyframe()
    {
        // Four one-second stretches of 400 bytes: a 300 byte keyframe followed by 100 bytes of audio
        List<(double Time, long Size, bool IsKeyframe)> packets = [];
        for (int second = 0; second < 4; second++)
        {
            packets.Add((second, 300, true));
            packets.Add((second + 0.5, 100, false));
        }

        List<double> cutTimes = VideoSplicer.PlanCutTimes(packets, maxBytes: 1000);

        cutTimes.Should().Equal(2.0);
    }

    [Fact]
    public void PlanCutTimes_StretchLargerThanLimit_ThrowsSpliceException()
    {
        List<(double Time, long Size, bool IsKeyframe)> packets = [(0, 100, true), (1, 2000, true)];

        Action plan = () => VideoSplicer.PlanCutTimes(packets, maxBytes: 1000);

        plan.Should().Throw<SpliceException>();
    }
}
