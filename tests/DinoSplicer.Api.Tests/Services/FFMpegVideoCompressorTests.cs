using DinoSplicer.Api.Exceptions;
using DinoSplicer.Api.Models;
using DinoSplicer.Api.Services;

using FFMpegCore;

using FluentAssertions;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace DinoSplicer.Api.Tests.Services;

public class FFMpegVideoCompressorTests : IDisposable
{
    private static readonly string CheckedInSamplePath = Path.Combine(AppContext.BaseDirectory, "assets", "sample_320x240.mp4");

    private readonly FFProbeVideoMetadataExtractor _metadataExtractor = new();
    private readonly RecordingProgressTracker _progressTracker = new();
    private readonly SystemFileSystem _fileSystem = new();
    private readonly FFMpegVideoCompressor _compressor;
    private readonly string _outputDirectory = Path.Combine(Path.GetTempPath(), "dinosplicer-tests", Guid.NewGuid().ToString("N"));

    public FFMpegVideoCompressorTests()
    {
        _compressor = new FFMpegVideoCompressor(
            _metadataExtractor,
            _progressTracker,
            _fileSystem,
            NullLogger<FFMpegVideoCompressor>.Instance
        );
    }

    public void Dispose()
    {
        if (Directory.Exists(_outputDirectory))
        {
            Directory.Delete(_outputDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task CompressAsync_SampleMp4WithScaleAndCrf_ProducesValidMp4WithExpectedResolutionCodecAndProgress()
    {
        Guid jobId = Guid.NewGuid();
        string outputDir = Path.Combine(_outputDirectory, "mp4-scaled");
        CompressionRequest request = new()
        {
            Format = OutputContainerFormat.Mp4,
            VideoCodec = VideoCodecOption.H264,
            Crf = 32,
            TargetWidth = 160,
            TargetHeight = 120,
            AudioBitrateKbps = 96,
            Preset = EncodingPresetOption.Fast,
        };

        CompressionResult result = await _compressor.CompressAsync(jobId, CheckedInSamplePath, outputDir, request);

        Path.GetExtension(result.FilePath).Should().Be(".mp4");
        File.Exists(result.FilePath).Should().BeTrue();
        result.SizeBytes.Should().Be(new FileInfo(result.FilePath).Length);
        result.DurationSeconds.Should().BeApproximately(1.0, 0.15);

        IMediaAnalysis analysis = await FFProbe.AnalyseAsync(result.FilePath);
        analysis.PrimaryVideoStream.Should().NotBeNull();
        analysis.PrimaryVideoStream!.Width.Should().Be(160);
        analysis.PrimaryVideoStream.Height.Should().Be(120);
        analysis.PrimaryVideoStream.CodecName.Should().Be("h264");

        IReadOnlyList<int> progress = _progressTracker.GetReports(jobId);
        progress.Should().NotBeEmpty();
        progress.First().Should().Be(0);
        progress.Last().Should().Be(100);
        progress.Should().OnlyContain(value => value >= 0 && value <= 100);
        progress.Should().BeInAscendingOrder();

        await AssertPlayableWithFfmpegDecodeAsync(result.FilePath);
    }

    [Fact]
    public async Task CompressAsync_SampleVideoToWebm_ProducesValidWebmWithVp9OpusAndProgress()
    {
        string sourcePath = await SampleVideo.GetPathAsync();
        Guid jobId = Guid.NewGuid();
        string outputDir = Path.Combine(_outputDirectory, "webm-scaled");
        CompressionRequest request = new()
        {
            Format = OutputContainerFormat.Webm,
            VideoCodec = VideoCodecOption.Vp9,
            Crf = 38,
            TargetWidth = 160,
            TargetHeight = 120,
            AudioBitrateKbps = 64,
            Preset = EncodingPresetOption.Ultrafast,
        };

        CompressionResult result = await _compressor.CompressAsync(
            jobId,
            sourcePath,
            outputDir,
            request,
            sourceDuration: TimeSpan.FromSeconds(SampleVideo.DurationSeconds)
        );

        Path.GetExtension(result.FilePath).Should().Be(".webm");
        File.Exists(result.FilePath).Should().BeTrue();
        result.SizeBytes.Should().Be(new FileInfo(result.FilePath).Length);
        result.DurationSeconds.Should().BeApproximately(SampleVideo.DurationSeconds, 0.2);

        IMediaAnalysis analysis = await FFProbe.AnalyseAsync(result.FilePath);
        analysis.PrimaryVideoStream.Should().NotBeNull();
        analysis.PrimaryVideoStream!.Width.Should().Be(160);
        analysis.PrimaryVideoStream.Height.Should().Be(120);
        analysis.PrimaryVideoStream.CodecName.Should().Be("vp9");
        analysis.PrimaryAudioStream.Should().NotBeNull();
        analysis.PrimaryAudioStream!.CodecName.Should().Be("opus");

        IReadOnlyList<int> progress = _progressTracker.GetReports(jobId);
        progress.Should().NotBeEmpty();
        progress.First().Should().Be(0);
        progress.Last().Should().Be(100);
        progress.Should().OnlyContain(value => value >= 0 && value <= 100);
        progress.Should().BeInAscendingOrder();

        await AssertPlayableWithFfmpegDecodeAsync(result.FilePath);
    }

    [Fact]
    public async Task CompressAsync_WithoutTargetDimensions_PreservesSourceResolution()
    {
        Guid jobId = Guid.NewGuid();
        string outputDir = Path.Combine(_outputDirectory, "preserve-resolution");
        CompressionRequest request = new()
        {
            Format = OutputContainerFormat.Mp4,
            VideoCodec = VideoCodecOption.H264,
            Crf = 28,
            TargetWidth = null,
            TargetHeight = null,
            AudioBitrateKbps = null,
            Preset = EncodingPresetOption.Ultrafast,
        };

        CompressionResult result = await _compressor.CompressAsync(jobId, CheckedInSamplePath, outputDir, request);

        IMediaAnalysis analysis = await FFProbe.AnalyseAsync(result.FilePath);
        analysis.PrimaryVideoStream.Should().NotBeNull();
        analysis.PrimaryVideoStream!.Width.Should().Be(320);
        analysis.PrimaryVideoStream.Height.Should().Be(240);
    }

    [Fact]
    public async Task CompressAsync_MissingOrCorruptInput_ThrowsMediaProcessingExceptionAndLeavesNoOrphanFiles()
    {
        CompressionRequest request = new();

        string missingSource = Path.Combine(_outputDirectory, "does-not-exist.mp4");
        string missingOutDir = Path.Combine(_outputDirectory, "missing-out");

        Func<Task> missingAct = () => _compressor.CompressAsync(
            Guid.NewGuid(),
            missingSource,
            missingOutDir,
            request,
            sourceDuration: TimeSpan.FromSeconds(1)
        );
        await missingAct.Should().ThrowAsync<MediaProcessingException>();
        Directory.Exists(missingOutDir).Should().BeFalse();

        Directory.CreateDirectory(_outputDirectory);
        string corruptSource = Path.Combine(_outputDirectory, "corrupt.mp4");
        string corruptOutDir = Path.Combine(_outputDirectory, "corrupt-out");
        await File.WriteAllTextAsync(corruptSource, "not a valid video");

        Func<Task> corruptAct = () => _compressor.CompressAsync(
            Guid.NewGuid(),
            corruptSource,
            corruptOutDir,
            request,
            sourceDuration: TimeSpan.FromSeconds(1)
        );
        await corruptAct.Should().ThrowAsync<MediaProcessingException>();
        Directory.Exists(corruptOutDir).Should().BeFalse();
    }

    [Fact]
    public async Task CompressAsync_PreExistingOutputDirectory_PreservesOtherFilesOnFailure()
    {
        Directory.CreateDirectory(_outputDirectory);
        string existingFile = Path.Combine(_outputDirectory, "keep.txt");
        await File.WriteAllTextAsync(existingFile, "keep-me");
        string missingSource = Path.Combine(_outputDirectory, "missing.mp4");

        Func<Task> act = () => _compressor.CompressAsync(
            Guid.NewGuid(),
            missingSource,
            _outputDirectory,
            new CompressionRequest(),
            sourceDuration: TimeSpan.FromSeconds(1)
        );

        await act.Should().ThrowAsync<MediaProcessingException>();
        File.Exists(existingFile).Should().BeTrue();
        File.Exists(Path.Combine(_outputDirectory, "compressed.mp4")).Should().BeFalse();
    }

    [Fact]
    public async Task CompressAsync_CancelledDuringEncoding_StopsCleanlyAndCleansPartialOutput()
    {
        string sourcePath = await SampleVideo.GetPathAsync();
        string outputDir = Path.Combine(_outputDirectory, "cancelled-out");
        using CancellationTokenSource cts = new();
        cts.CancelAfter(TimeSpan.FromMilliseconds(50));

        CompressionRequest request = new()
        {
            Format = OutputContainerFormat.Webm,
            VideoCodec = VideoCodecOption.Vp9,
            Crf = 30,
            Preset = EncodingPresetOption.Slow,
        };

        Func<Task> act = () => _compressor.CompressAsync(
            Guid.NewGuid(),
            sourcePath,
            outputDir,
            request,
            sourceDuration: TimeSpan.FromSeconds(SampleVideo.DurationSeconds),
            cancellationToken: cts.Token
        );

        await act.Should().ThrowAsync<OperationCanceledException>();
        Directory.Exists(outputDir).Should().BeFalse();
    }

    [Fact]
    public void BuildOutputArguments_Mp4H264WithScaleAndAudioBitrate_ContainsExpectedFlags()
    {
        CompressionRequest request = new()
        {
            Format = OutputContainerFormat.Mp4,
            VideoCodec = VideoCodecOption.H264,
            Crf = 28,
            TargetWidth = 640,
            TargetHeight = 360,
            AudioBitrateKbps = 128,
            Preset = EncodingPresetOption.Slow,
        };

        string args = FFMpegVideoCompressor.BuildOutputArguments(request);

        args.Should().Contain("-c:v libx264");
        args.Should().Contain("-crf 28");
        args.Should().Contain("-preset slow");
        args.Should().Contain("-vf scale=640:360");
        args.Should().Contain("-c:a aac");
        args.Should().Contain("-b:a 128k");
        args.Should().Contain("-movflags +faststart");
    }

    [Fact]
    public void BuildOutputArguments_WebmVp9WithoutOptionalParams_ContainsExpectedFlags()
    {
        CompressionRequest request = new()
        {
            Format = OutputContainerFormat.Webm,
            VideoCodec = VideoCodecOption.Vp9,
            Crf = 34,
            TargetWidth = null,
            TargetHeight = null,
            AudioBitrateKbps = null,
            Preset = EncodingPresetOption.Medium,
        };

        string args = FFMpegVideoCompressor.BuildOutputArguments(request);

        args.Should().Contain("-c:v libvpx-vp9");
        args.Should().Contain("-crf 34 -b:v 0");
        args.Should().Contain("-deadline good -cpu-used 2");
        args.Should().Contain("-c:a libopus");
        args.Should().NotContain("-vf scale=");
        args.Should().NotContain("-b:a");
        args.Should().NotContain("-movflags");
    }

    [Fact]
    public async Task CompressAsync_InvalidArguments_ThrowsArgumentException()
    {
        CompressionRequest halfDimensions = new() { TargetWidth = 160, TargetHeight = null };

        Func<Task> halfAct = () => _compressor.CompressAsync(Guid.NewGuid(), CheckedInSamplePath, _outputDirectory, halfDimensions);
        await halfAct.Should().ThrowAsync<ArgumentException>();

        CompressionRequest invalidCodec = new() { VideoCodec = (VideoCodecOption)999 };
        Func<Task> codecAct = () => _compressor.CompressAsync(Guid.NewGuid(), CheckedInSamplePath, _outputDirectory, invalidCodec);
        await codecAct.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ProgramDiContainer_ResolvesVideoCompressor()
    {
        using WebApplicationFactory<Program> factory = new();

        IVideoCompressor compressor = factory.Services.GetRequiredService<IVideoCompressor>();

        compressor.Should().BeOfType<FFMpegVideoCompressor>();
    }

    private static Task AssertPlayableWithFfmpegDecodeAsync(string filePath)
    {
        return FFMpegArguments
            .FromFileInput(filePath, verifyExists: false, options => options.WithCustomArgument("-v error"))
            .OutputToFile("-", overwrite: true, addArguments: options => options.ForceFormat("null"))
            .ProcessAsynchronously();
    }

    private sealed class RecordingProgressTracker : IJobProgressTracker
    {
        private readonly Lock _lock = new();
        private readonly Dictionary<Guid, List<int>> _history = [];

        public void ReportProgress(Guid jobId, int progress)
        {
            lock (_lock)
            {
                if (!_history.TryGetValue(jobId, out List<int>? list))
                {
                    list = [];
                    _history[jobId] = list;
                }

                list.Add(progress);
            }
        }

        public int? GetProgress(Guid jobId)
        {
            lock (_lock)
            {
                return _history.TryGetValue(jobId, out List<int>? list) && list.Count > 0 ? list[^1] : null;
            }
        }

        public bool Remove(Guid jobId)
        {
            lock (_lock)
            {
                return _history.Remove(jobId);
            }
        }

        public IReadOnlyList<int> GetReports(Guid jobId)
        {
            lock (_lock)
            {
                return _history.TryGetValue(jobId, out List<int>? list) ? [.. list] : [];
            }
        }
    }
}
