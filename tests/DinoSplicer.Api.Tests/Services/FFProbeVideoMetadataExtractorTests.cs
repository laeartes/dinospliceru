using DinoSplicer.Api.Models;
using DinoSplicer.Api.Services;

using FFMpegCore;
using FFMpegCore.Builders.MetaData;

using FluentAssertions;

namespace DinoSplicer.Api.Tests.Services;

public class FFProbeVideoMetadataExtractorTests
{
    private sealed class FakeMediaAnalysis(TimeSpan duration, VideoStream? primaryVideoStream) : IMediaAnalysis
    {
        public TimeSpan Duration { get; } = duration;
        public MediaFormat Format { get; } = new();
        public List<ChapterData> Chapters { get; } = [];
        public AudioStream? PrimaryAudioStream => null;
        public VideoStream? PrimaryVideoStream { get; } = primaryVideoStream;
        public SubtitleStream? PrimarySubtitleStream => null;
        public List<AudioStream> AudioStreams { get; } = [];
        public List<VideoStream> VideoStreams { get; } = [];
        public List<SubtitleStream> SubtitleStreams { get; } = [];
        public IReadOnlyList<string> ErrorData { get; } = [];
    }

    private static FakeMediaAnalysis AnalysisOf(int width, int height, string codec, TimeSpan duration) =>
        new(duration, new VideoStream { Width = width, Height = height, CodecName = codec });

    private static FFProbeVideoMetadataExtractor ExtractorReturning(IMediaAnalysis analysis) =>
        new((_, _) => Task.FromResult<IMediaAnalysis>(analysis));

    [Fact]
    public async Task ExtractMetadata_ValidAnalysis_MapsDurationResolutionAndCodec()
    {
        var extractor = ExtractorReturning(AnalysisOf(1920, 1080, "h264", TimeSpan.FromSeconds(12.5)));

        VideoMetadata metadata = await extractor.ExtractMetadata(filePath: "a.mp4", extractCodec: true);

        metadata.Duration.Should().Be(TimeSpan.FromSeconds(12.5));
        metadata.Resolution.Width.Should().Be(1920);
        metadata.Resolution.Height.Should().Be(1080);
        metadata.Codec.Should().Be("h264");
    }

    [Fact]
    public async Task ExtractMetadata_ExtractCodecFalse_LeavesCodecNull()
    {
        var extractor = ExtractorReturning(AnalysisOf(640, 360, "vp9", TimeSpan.FromSeconds(1)));

        VideoMetadata metadata = await extractor.ExtractMetadata(filePath: "a.webm", extractCodec: false);

        metadata.Codec.Should().BeNull();
        metadata.Resolution.Width.Should().Be(640);
    }

    [Fact]
    public async Task ExtractMetadata_NoVideoStream_ThrowsInvalidOperationException()
    {
        var extractor = ExtractorReturning(new FakeMediaAnalysis(TimeSpan.FromSeconds(3), primaryVideoStream: null));

        Func<Task> act = () => extractor.ExtractMetadata(filePath: "audio-only.mp4");

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ExtractMetadata_AnalyserExceedsTimeout_ThrowsTimeoutException()
    {
        FFProbeVideoMetadataExtractor extractor = new(async (_, token) =>
        {
            await Task.Delay(Timeout.Infinite, token);
            return null!;
        });

        Func<Task> act = () => extractor.ExtractMetadata(filePath: "slow.mp4", timeout: TimeSpan.FromMilliseconds(50));

        await act.Should().ThrowAsync<TimeoutException>();
    }

    [Fact]
    public async Task ExtractMetadata_CallerCancels_ThrowsOperationCanceledException()
    {
        using CancellationTokenSource cts = new();
        FFProbeVideoMetadataExtractor extractor = new(async (_, token) =>
        {
            await Task.Delay(Timeout.Infinite, token);
            return null!;
        });

        Task<VideoMetadata> task = extractor.ExtractMetadata(filePath: "a.mp4", cancellationToken: cts.Token);
        await cts.CancelAsync();

        await task.Invoking(t => t).Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ExtractMetadata_AnalyserThrows_PropagatesException()
    {
        FFProbeVideoMetadataExtractor extractor = new((_, _) => throw new InvalidOperationException("ffprobe failed"));

        Func<Task> act = () => extractor.ExtractMetadata(filePath: "corrupt.mp4");

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("ffprobe failed");
    }
}
