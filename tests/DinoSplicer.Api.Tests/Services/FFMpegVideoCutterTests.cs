using DinoSplicer.Api.Exceptions;
using DinoSplicer.Api.Models;
using DinoSplicer.Api.Services;

using FFMpegCore;

using FluentAssertions;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace DinoSplicer.Api.Tests.Services;

public class FFMpegVideoCutterTests : IDisposable
{
    private static readonly string CheckedInSamplePath = Path.Combine(AppContext.BaseDirectory, "assets", "sample_320x240.mp4");

    private readonly FFMpegRunner _ffmpegRunner = new();
    private readonly FFProbeVideoMetadataExtractor _metadataExtractor = new();
    private readonly RecordingProgressTracker _progressTracker = new();
    private readonly SystemFileSystem _fileSystem = new();
    private readonly FFMpegVideoCutter _cutter;
    private readonly string _outputDirectory = Path.Combine(Path.GetTempPath(), "dinosplicer-tests", Guid.NewGuid().ToString("N"));

    public FFMpegVideoCutterTests()
    {
        _cutter = new FFMpegVideoCutter(
            _ffmpegRunner,
            _metadataExtractor,
            _progressTracker,
            _fileSystem,
            NullLogger<FFMpegVideoCutter>.Instance
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
    public async Task CutAsync_CheckedInSilentSample_ProducesPlayableClipsInBothModes()
    {
        // Confirm the checked-in fixture has no audio stream so -map 0:a? optional mapping is exercised
        IMediaAnalysis sourceAnalysis = await FFProbe.AnalyseAsync(CheckedInSamplePath);
        sourceAnalysis.PrimaryAudioStream.Should().BeNull();

        string fastDir = Path.Combine(_outputDirectory, "fast");
        string accurateDir = Path.Combine(_outputDirectory, "accurate");
        Guid fastJobId = Guid.NewGuid();
        Guid accurateJobId = Guid.NewGuid();

        CutResult fastResult = await _cutter.CutAsync(fastJobId, CheckedInSamplePath, fastDir, 0.0, 1.0, CutMode.FastCopy);
        CutResult accurateResult = await _cutter.CutAsync(accurateJobId, CheckedInSamplePath, accurateDir, 0.1, 0.7, CutMode.FrameAccurate);

        File.Exists(fastResult.FilePath).Should().BeTrue();
        fastResult.SizeBytes.Should().Be(new FileInfo(fastResult.FilePath).Length);
        fastResult.DurationSeconds.Should().BeApproximately(1.0, 0.1);
        _progressTracker.GetReports(fastJobId).Should().BeEmpty();
        await AssertPlayableWithFfmpegDecodeAsync(fastResult.FilePath);

        File.Exists(accurateResult.FilePath).Should().BeTrue();
        accurateResult.SizeBytes.Should().Be(new FileInfo(accurateResult.FilePath).Length);
        accurateResult.DurationSeconds.Should().BeApproximately(0.6, 0.08);
        IReadOnlyList<int> progress = _progressTracker.GetReports(accurateJobId);
        progress.Should().NotBeEmpty();
        progress.First().Should().Be(0);
        progress.Last().Should().Be(100);
        progress.Should().OnlyContain(value => value >= 0 && value <= 100);
        progress.Should().BeInAscendingOrder();
        await AssertPlayableWithFfmpegDecodeAsync(accurateResult.FilePath);
    }

    [Fact]
    public async Task CutAsync_GeneratedVideoWithAudio_CutsWithinTightToleranceInBothModes()
    {
        string sourcePath = await SampleVideo.GetPathAsync();
        string fastDir = Path.Combine(_outputDirectory, "fast");
        string accurateDir = Path.Combine(_outputDirectory, "accurate");
        Guid accurateJobId = Guid.NewGuid();

        // SampleVideo has keyframes every 1.0 s (-g 30 at 30 fps), so [2.0, 5.0] aligns on keyframe boundaries
        CutResult fastResult = await _cutter.CutAsync(Guid.NewGuid(), sourcePath, fastDir, 2.0, 5.0, CutMode.FastCopy);
        CutResult accurateResult = await _cutter.CutAsync(accurateJobId, sourcePath, accurateDir, 2.25, 4.75, CutMode.FrameAccurate);

        // Stream-copied AAC audio carries packet priming/interleave overhang (~0.14 s) beyond the 3.0 s video keyframes
        fastResult.DurationSeconds.Should().BeApproximately(3.0, 0.25);
        await AssertPlayableWithFfmpegDecodeAsync(fastResult.FilePath);

        accurateResult.DurationSeconds.Should().BeApproximately(2.5, 0.08);
        _progressTracker.GetProgress(accurateJobId).Should().Be(100);
        await AssertPlayableWithFfmpegDecodeAsync(accurateResult.FilePath);
    }

    [Fact]
    public async Task CutAsync_WebmSource_ReencodesWithVp9AndOpusInFrameAccurateMode()
    {
        Directory.CreateDirectory(_outputDirectory);
        string webmSource = Path.Combine(_outputDirectory, "source.webm");
        await FFMpegArguments
            .FromFileInput("testsrc2=size=160x120:rate=24:duration=2", verifyExists: false, options => options.ForceFormat("lavfi"))
            .AddFileInput("sine=frequency=440:duration=2", verifyExists: false, options => options.ForceFormat("lavfi"))
            .OutputToFile(webmSource, overwrite: true, options => options
                .WithCustomArgument("-c:v libvpx-vp9 -crf 35 -b:v 0 -deadline realtime -cpu-used 8 -c:a libopus -shortest"))
            .ProcessAsynchronously();

        string outputDir = Path.Combine(_outputDirectory, "webm-out");
        Guid jobId = Guid.NewGuid();

        CutResult result = await _cutter.CutAsync(jobId, webmSource, outputDir, 0.25, 1.25, CutMode.FrameAccurate);

        Path.GetExtension(result.FilePath).Should().Be(".webm");
        result.DurationSeconds.Should().BeApproximately(1.0, 0.1);
        _progressTracker.GetProgress(jobId).Should().Be(100);
        await AssertPlayableWithFfmpegDecodeAsync(result.FilePath);
    }

    [Fact]
    public async Task CutAsync_EndSecondsBeyondSourceDuration_ClampsToEndOfStreamCleanly()
    {
        string outputDir = Path.Combine(_outputDirectory, "beyond-eof");

        // Checked-in sample is 1.0 s long; requesting [0.4, 5.0] produces the remaining ~0.6 s
        CutResult result = await _cutter.CutAsync(Guid.NewGuid(), CheckedInSamplePath, outputDir, 0.4, 5.0, CutMode.FrameAccurate);

        result.DurationSeconds.Should().BeApproximately(0.6, 0.1);
        await AssertPlayableWithFfmpegDecodeAsync(result.FilePath);
    }

    [Fact]
    public async Task CutAsync_ExistingFilesInOutputDirectory_PreservesPreExistingFilesOnSuccessAndFailure()
    {
        Directory.CreateDirectory(_outputDirectory);
        string existingFile = Path.Combine(_outputDirectory, "keep.txt");
        await File.WriteAllTextAsync(existingFile, "important data");

        CutResult success = await _cutter.CutAsync(Guid.NewGuid(), CheckedInSamplePath, _outputDirectory, 0.0, 0.5, CutMode.FastCopy);
        File.Exists(success.FilePath).Should().BeTrue();
        (await File.ReadAllTextAsync(existingFile)).Should().Be("important data");

        string missingSource = Path.Combine(_outputDirectory, "missing.mp4");
        Func<Task> failedCut = () => _cutter.CutAsync(Guid.NewGuid(), missingSource, _outputDirectory, 0.0, 0.5, CutMode.FastCopy);

        await failedCut.Should().ThrowAsync<MediaProcessingException>();
        File.Exists(existingFile).Should().BeTrue();
        (await File.ReadAllTextAsync(existingFile)).Should().Be("important data");
        Directory.Exists(_outputDirectory).Should().BeTrue();
    }

    [Fact]
    public async Task CutAsync_MissingOrCorruptInput_ThrowsMediaProcessingExceptionAndCleansCreatedDirectory()
    {
        string missingSource = Path.Combine(_outputDirectory, "does-not-exist.mp4");
        string missingOutDir = Path.Combine(_outputDirectory, "missing-out");

        Func<Task> missingAct = () => _cutter.CutAsync(Guid.NewGuid(), missingSource, missingOutDir, 0.0, 0.5, CutMode.FastCopy);
        await missingAct.Should().ThrowAsync<MediaProcessingException>();
        Directory.Exists(missingOutDir).Should().BeFalse();

        Directory.CreateDirectory(_outputDirectory);
        string corruptSource = Path.Combine(_outputDirectory, "corrupt.mp4");
        string corruptOutDir = Path.Combine(_outputDirectory, "corrupt-out");
        await File.WriteAllTextAsync(corruptSource, "not a valid video");

        Func<Task> corruptAct = () => _cutter.CutAsync(Guid.NewGuid(), corruptSource, corruptOutDir, 0.0, 0.5, CutMode.FrameAccurate);
        await corruptAct.Should().ThrowAsync<MediaProcessingException>();
        Directory.Exists(corruptOutDir).Should().BeFalse();
    }

    [Fact]
    public async Task CutAsync_CancelledToken_CleansUpPartialOutputAndRethrowsOperationCanceledException()
    {
        using CancellationTokenSource cts = new();
        Guid jobId = Guid.NewGuid();
        string outputDir = Path.Combine(_outputDirectory, "cancelled-out");

        FFMpegVideoCutter cutter = new(
            new CancellingRunner(cts),
            _metadataExtractor,
            _progressTracker,
            _fileSystem,
            NullLogger<FFMpegVideoCutter>.Instance
        );

        Func<Task> act = () => cutter.CutAsync(jobId, "source.mp4", outputDir, 0.0, 2.0, CutMode.FrameAccurate, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        Directory.Exists(outputDir).Should().BeFalse();
    }

    [Fact]
    public async Task CutAsync_OutOfBoundsAndOutOfOrderProgress_ClampsAndMaintainsMonotonicSequence()
    {
        Guid jobId = Guid.NewGuid();
        string outputDir = Path.Combine(_outputDirectory, "progress-out");

        FFMpegVideoCutter cutter = new(
            new ScriptedProgressRunner([
                TimeSpan.FromSeconds(-0.5),
                TimeSpan.FromSeconds(1.0),
                TimeSpan.FromSeconds(0.5),
                TimeSpan.FromSeconds(3.5),
            ]),
            new StubMetadataExtractor(TimeSpan.FromSeconds(2.0)),
            _progressTracker,
            _fileSystem,
            NullLogger<FFMpegVideoCutter>.Instance
        );

        CutResult result = await cutter.CutAsync(jobId, "source.mp4", outputDir, 1.0, 3.0, CutMode.FrameAccurate);

        result.DurationSeconds.Should().Be(2.0);
        IReadOnlyList<int> reports = _progressTracker.GetReports(jobId);
        reports.Should().Equal(0, 50, 100);
    }

    [Fact]
    public async Task CutAsync_CleanupThrowsAfterFailure_DoesNotMaskMediaProcessingException()
    {
        FFMpegVideoCutter cutter = new(
            new ThrowingRunner(),
            _metadataExtractor,
            _progressTracker,
            new ThrowingCleanupFileSystem(),
            NullLogger<FFMpegVideoCutter>.Instance
        );

        Func<Task> act = () => cutter.CutAsync(Guid.NewGuid(), "source.mp4", _outputDirectory, 0.0, 1.0, CutMode.FastCopy);

        await act.Should().ThrowAsync<MediaProcessingException>()
            .WithMessage("The video clip could not be cut.");
    }

    [Theory]
    [InlineData(-0.1, 1.0, CutMode.FastCopy)]
    [InlineData(double.NaN, 1.0, CutMode.FastCopy)]
    [InlineData(1.0, 1.0, CutMode.FastCopy)]
    [InlineData(2.0, 1.0, CutMode.FrameAccurate)]
    [InlineData(0.0, 1.0, (CutMode)999)]
    public async Task CutAsync_InvalidArguments_ThrowsArgumentOutOfRangeException(double start, double end, CutMode mode)
    {
        Func<Task> act = () => _cutter.CutAsync(Guid.NewGuid(), "source.mp4", _outputDirectory, start, end, mode);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ProgramDiContainer_ResolvesVideoCutterAndDependencies()
    {
        using WebApplicationFactory<Program> factory = new();

        IVideoCutter cutter = factory.Services.GetRequiredService<IVideoCutter>();
        IFFMpegRunner runner = factory.Services.GetRequiredService<IFFMpegRunner>();
        IFileSystem fileSystem = factory.Services.GetRequiredService<IFileSystem>();

        cutter.Should().BeOfType<FFMpegVideoCutter>();
        runner.Should().BeOfType<FFMpegRunner>();
        fileSystem.Should().BeOfType<SystemFileSystem>();
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

    private sealed class CancellingRunner(CancellationTokenSource cts) : IFFMpegRunner
    {
        public async Task RunCutAsync(
            string sourcePath,
            string outputPath,
            double startSeconds,
            double endSeconds,
            CutMode mode,
            Action<TimeSpan>? onProgress = null,
            CancellationToken cancellationToken = default
        )
        {
            await File.WriteAllTextAsync(outputPath, "partial", CancellationToken.None);
            await cts.CancelAsync();
            cancellationToken.ThrowIfCancellationRequested();
        }
    }

    private sealed class ScriptedProgressRunner(IReadOnlyList<TimeSpan> ticks) : IFFMpegRunner
    {
        public async Task RunCutAsync(
            string sourcePath,
            string outputPath,
            double startSeconds,
            double endSeconds,
            CutMode mode,
            Action<TimeSpan>? onProgress = null,
            CancellationToken cancellationToken = default
        )
        {
            foreach (TimeSpan tick in ticks)
            {
                onProgress?.Invoke(tick);
            }

            await File.WriteAllTextAsync(outputPath, "fake-video-bytes", cancellationToken);
        }
    }

    private sealed class ThrowingRunner : IFFMpegRunner
    {
        public Task RunCutAsync(
            string sourcePath,
            string outputPath,
            double startSeconds,
            double endSeconds,
            CutMode mode,
            Action<TimeSpan>? onProgress = null,
            CancellationToken cancellationToken = default
        )
        {
            throw new InvalidOperationException("ffmpeg exited with code 1");
        }
    }

    private sealed class StubMetadataExtractor(TimeSpan duration) : IVideoMetadataExtractor
    {
        public Task<VideoMetadata> ExtractMetadata(
            string filePath,
            bool extractCodec = true,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default
        )
        {
            return Task.FromResult(new VideoMetadata(duration, new Resolution(320, 240), "h264"));
        }
    }

    private sealed class ThrowingCleanupFileSystem : IFileSystem
    {
        public Task<bool> CreateDirectoryIfNotExistsAsync(string directoryPath, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<long> GetFileLengthAsync(string filePath, CancellationToken cancellationToken = default) => Task.FromResult(0L);
        public Task DeleteFileIfExistsAsync(string filePath, CancellationToken cancellationToken = default) => throw new IOException("file locked");
        public Task DeleteDirectoryRecursiveAsync(string directoryPath, CancellationToken cancellationToken = default) => throw new IOException("directory locked");
        public Task DeleteDirectoryIfEmptyAsync(string directoryPath, CancellationToken cancellationToken = default) => throw new IOException("directory locked");
        public Task<bool> FileExistsAsync(string filePath, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> DirectoryHasEntriesAsync(string directoryPath, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<IReadOnlyList<string>> EnumerateFilesAsync(string directoryPath, string searchPattern, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<string>>([]);
    }
}
