using System.Globalization;

using DinoSplicer.Api.Exceptions;
using DinoSplicer.Api.Models;

using FFMpegCore;

namespace DinoSplicer.Api.Services;

public class FFMpegVideoCompressor(
    IVideoMetadataExtractor metadataExtractor,
    IJobProgressTracker progressTracker,
    IFileSystem fileSystem,
    ILogger<FFMpegVideoCompressor> logger
) : IVideoCompressor
{
    private const string OutputFileBaseName = "compressed";
    public const int MinTargetVideoBitrateKbps = 50;
    private const int DefaultAudioBitrateKbps = 128;
    private const double ContainerOverheadFactor = 0.90;

    private readonly IVideoMetadataExtractor _metadataExtractor = metadataExtractor ?? throw new ArgumentNullException(nameof(metadataExtractor));
    private readonly IJobProgressTracker _progressTracker = progressTracker ?? throw new ArgumentNullException(nameof(progressTracker));
    private readonly IFileSystem _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
    private readonly ILogger<FFMpegVideoCompressor> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public async Task<CompressionResult> CompressAsync(
        Guid jobId,
        string sourcePath,
        string outputDirectory,
        CompressionRequest request,
        TimeSpan? sourceDuration = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        ArgumentNullException.ThrowIfNull(request);

        ValidateRequest(request);

        bool directoryCreated = await _fileSystem.CreateDirectoryIfNotExistsAsync(outputDirectory, cancellationToken);
        string outputPath = Path.Combine(outputDirectory, $"{OutputFileBaseName}{GetFileExtension(request.Format)}");

        Lock progressLock = new();
        int maxReportedProgress = -1;

        void ReportMonotonicProgress(int candidate)
        {
            int clamped = Math.Clamp(candidate, 0, 100);
            int toReport;
            lock (progressLock)
            {
                if (clamped <= maxReportedProgress)
                {
                    return;
                }

                maxReportedProgress = clamped;
                toReport = clamped;
            }

            _progressTracker.ReportProgress(jobId, toReport);
        }

        try
        {
            TimeSpan totalDuration = await ResolveSourceDurationAsync(sourcePath, sourceDuration, cancellationToken);

            ReportMonotonicProgress(0);

            if (request.TargetSizeBytes is long targetSizeBytes)
            {
                int videoBitrateKbps = CalculateTargetVideoBitrateKbps(
                    targetSizeBytes,
                    totalDuration,
                    request.AudioBitrateKbps
                );

                if (videoBitrateKbps < MinTargetVideoBitrateKbps)
                {
                    throw new MediaProcessingException(
                        "Target size is too small for this video's duration and audio bitrate."
                    );
                }

                string passLogPrefix = Path.Combine(outputDirectory, $"{OutputFileBaseName}_passlog");
                string nullSink = OperatingSystem.IsWindows() ? "NUL" : "/dev/null";

                try
                {
                    string pass1Arguments = BuildOutputArguments(
                        request,
                        targetVideoBitrateKbps: videoBitrateKbps,
                        pass: 1,
                        passLogPrefix: passLogPrefix
                    );

                    await FFMpegArguments
                        .FromFileInput(sourcePath, verifyExists: false)
                        .OutputToFile(nullSink, overwrite: true, addArguments: options => options
                            .WithCustomArgument(pass1Arguments))
                        .NotifyOnProgress(percent =>
                        {
                            if (double.IsFinite(percent))
                            {
                                ReportMonotonicProgress((int)Math.Round(percent * 0.5));
                            }
                        }, totalDuration)
                        .CancellableThrough(cancellationToken)
                        .ProcessAsynchronously();

                    string pass2Arguments = BuildOutputArguments(
                        request,
                        targetVideoBitrateKbps: videoBitrateKbps,
                        pass: 2,
                        passLogPrefix: passLogPrefix
                    );

                    await FFMpegArguments
                        .FromFileInput(sourcePath, verifyExists: false)
                        .OutputToFile(outputPath, overwrite: true, addArguments: options => options
                            .WithCustomArgument(pass2Arguments))
                        .NotifyOnProgress(percent =>
                        {
                            if (double.IsFinite(percent))
                            {
                                ReportMonotonicProgress(50 + (int)Math.Round(percent * 0.5));
                            }
                        }, totalDuration)
                        .CancellableThrough(cancellationToken)
                        .ProcessAsynchronously();
                }
                finally
                {
                    await CleanupPassLogsAsync(outputDirectory, $"{OutputFileBaseName}_passlog");
                }
            }
            else
            {
                string outputArguments = BuildOutputArguments(request);

                await FFMpegArguments
                    .FromFileInput(sourcePath, verifyExists: false)
                    .OutputToFile(outputPath, overwrite: true, addArguments: options => options
                        .WithCustomArgument(outputArguments))
                    .NotifyOnProgress(percent =>
                    {
                        if (double.IsFinite(percent))
                        {
                            ReportMonotonicProgress((int)Math.Round(percent));
                        }
                    }, totalDuration)
                    .CancellableThrough(cancellationToken)
                    .ProcessAsynchronously();
            }

            VideoMetadata outputMetadata = await _metadataExtractor.ExtractMetadata(
                filePath: outputPath,
                extractCodec: false,
                cancellationToken: cancellationToken
            );

            if (outputMetadata.Duration <= TimeSpan.Zero)
            {
                throw new MediaProcessingException("The compressed video has zero duration.");
            }

            long sizeBytes = await _fileSystem.GetFileLengthAsync(outputPath, cancellationToken);
            if (sizeBytes <= 0)
            {
                throw new MediaProcessingException("The compressed video is empty.");
            }

            ReportMonotonicProgress(100);

            return new CompressionResult(outputPath, outputMetadata.Duration.TotalSeconds, sizeBytes);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Video compression cancelled for job {JobId}", jobId);
            await TryCleanupAsync(outputPath, outputDirectory, directoryCreated);
            throw;
        }
        catch (MediaProcessingException ex)
        {
            _logger.LogWarning(ex, "Video compression validation failed for job {JobId}", jobId);
            await TryCleanupAsync(outputPath, outputDirectory, directoryCreated);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "FFmpeg video compression failed for job {JobId}", jobId);
            await TryCleanupAsync(outputPath, outputDirectory, directoryCreated);
            throw new MediaProcessingException("The video could not be compressed.", innerException: ex);
        }
    }

    public static int CalculateTargetVideoBitrateKbps(
        long targetSizeBytes,
        TimeSpan duration,
        int? audioBitrateKbps
    )
    {
        if (targetSizeBytes <= 0 || duration <= TimeSpan.Zero)
        {
            return 0;
        }

        double totalKbps = (targetSizeBytes * 8.0 / duration.TotalSeconds / 1000.0) * ContainerOverheadFactor;
        int audioKbps = audioBitrateKbps ?? DefaultAudioBitrateKbps;
        return (int)Math.Floor(totalKbps - audioKbps);
    }

    public static string BuildOutputArguments(
        CompressionRequest request,
        int? targetVideoBitrateKbps = null,
        int? pass = null,
        string? passLogPrefix = null
    )
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateRequest(request);

        List<string> args = pass == 1 ? ["-map 0:v:0"] : ["-map 0:v:0", "-map 0:a?"];

        if (request.TargetWidth is int width && request.TargetHeight is int height)
        {
            args.Add($"-vf scale={width.ToString(CultureInfo.InvariantCulture)}:{height.ToString(CultureInfo.InvariantCulture)}");
        }

        if (targetVideoBitrateKbps is int bitrateKbps)
        {
            string bitrate = bitrateKbps.ToString(CultureInfo.InvariantCulture);
            string bufSize = (bitrateKbps * 2).ToString(CultureInfo.InvariantCulture);

            switch (request.VideoCodec)
            {
                case VideoCodecOption.H264:
                    args.Add($"-c:v libx264 -b:v {bitrate}k -maxrate {bitrate}k -bufsize {bufSize}k -preset {GetH264Preset(request.Preset)} -pix_fmt yuv420p");
                    break;

                case VideoCodecOption.Vp9:
                    args.Add($"-c:v libvpx-vp9 -b:v {bitrate}k {GetVp9SpeedArguments(request.Preset)} -pix_fmt yuv420p");
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(request), request.VideoCodec, "Unsupported video codec.");
            }
        }
        else
        {
            string crf = request.Crf.ToString(CultureInfo.InvariantCulture);

            switch (request.VideoCodec)
            {
                case VideoCodecOption.H264:
                    args.Add($"-c:v libx264 -crf {crf} -preset {GetH264Preset(request.Preset)} -pix_fmt yuv420p");
                    break;

                case VideoCodecOption.Vp9:
                    args.Add($"-c:v libvpx-vp9 -crf {crf} -b:v 0 {GetVp9SpeedArguments(request.Preset)} -pix_fmt yuv420p");
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(request), request.VideoCodec, "Unsupported video codec.");
            }
        }

        if (pass is int passNumber)
        {
            args.Add($"-pass {passNumber.ToString(CultureInfo.InvariantCulture)}");
            if (!string.IsNullOrWhiteSpace(passLogPrefix))
            {
                args.Add($"-passlogfile \"{passLogPrefix}\"");
            }
        }

        if (pass == 1)
        {
            args.Add("-an -f null");
            return string.Join(' ', args);
        }

        string audioCodec = request.Format switch
        {
            OutputContainerFormat.Mp4 => "aac",
            OutputContainerFormat.Webm => "libopus",
            _ => throw new ArgumentOutOfRangeException(nameof(request), request.Format, "Unsupported container format."),
        };

        args.Add($"-c:a {audioCodec}");

        if (request.AudioBitrateKbps is int audioBitrateKbps)
        {
            args.Add($"-b:a {audioBitrateKbps.ToString(CultureInfo.InvariantCulture)}k");
        }
        else if (targetVideoBitrateKbps.HasValue)
        {
            args.Add($"-b:a {DefaultAudioBitrateKbps.ToString(CultureInfo.InvariantCulture)}k");
        }

        if (request.Format == OutputContainerFormat.Mp4)
        {
            args.Add("-movflags +faststart");
        }

        return string.Join(' ', args);
    }

    private async Task CleanupPassLogsAsync(string outputDirectory, string filePrefix)
    {
        try
        {
            if (!Directory.Exists(outputDirectory))
            {
                return;
            }

            foreach (string file in Directory.EnumerateFiles(outputDirectory, $"{filePrefix}*"))
            {
                await _fileSystem.DeleteFileIfExistsAsync(file, CancellationToken.None);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to clean up FFmpeg 2-pass log files in {OutputDirectory}", outputDirectory);
        }
    }

    private async Task<TimeSpan> ResolveSourceDurationAsync(
        string sourcePath,
        TimeSpan? sourceDuration,
        CancellationToken cancellationToken
    )
    {
        if (sourceDuration is TimeSpan provided && provided > TimeSpan.Zero)
        {
            return provided;
        }

        VideoMetadata metadata = await _metadataExtractor.ExtractMetadata(
            filePath: sourcePath,
            extractCodec: false,
            cancellationToken: cancellationToken
        );

        if (metadata.Duration <= TimeSpan.Zero)
        {
            throw new MediaProcessingException("The source video has zero duration.");
        }

        return metadata.Duration;
    }

    private async Task TryCleanupAsync(string outputPath, string outputDirectory, bool directoryCreated)
    {
        try
        {
            await _fileSystem.DeleteFileIfExistsAsync(outputPath, CancellationToken.None);

            if (directoryCreated)
            {
                await _fileSystem.DeleteDirectoryRecursiveAsync(outputDirectory, CancellationToken.None);
            }
            else
            {
                await _fileSystem.DeleteDirectoryIfEmptyAsync(outputDirectory, CancellationToken.None);
            }
        }
        catch (Exception cleanupEx)
        {
            _logger.LogWarning(cleanupEx, "Failed to clean up partial compression output at {OutputPath}", outputPath);
        }
    }

    private static void ValidateRequest(CompressionRequest request)
    {
        if (!Enum.IsDefined(request.Format))
        {
            throw new ArgumentOutOfRangeException(nameof(request), request.Format, "Unsupported container format.");
        }

        if (!Enum.IsDefined(request.VideoCodec))
        {
            throw new ArgumentOutOfRangeException(nameof(request), request.VideoCodec, "Unsupported video codec.");
        }

        if (!Enum.IsDefined(request.Preset))
        {
            throw new ArgumentOutOfRangeException(nameof(request), request.Preset, "Unsupported encoding preset.");
        }

        if (request.TargetWidth.HasValue != request.TargetHeight.HasValue)
        {
            throw new ArgumentException("TargetWidth and TargetHeight must both be specified or both be omitted.", nameof(request));
        }

        if (request.TargetWidth is <= 0 || request.TargetHeight is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Target dimensions must be positive integers.");
        }
    }

    private static string GetFileExtension(OutputContainerFormat format)
    {
        return format switch
        {
            OutputContainerFormat.Mp4 => ".mp4",
            OutputContainerFormat.Webm => ".webm",
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unsupported container format."),
        };
    }

    private static string GetH264Preset(EncodingPresetOption preset)
    {
        return preset switch
        {
            EncodingPresetOption.Ultrafast => "ultrafast",
            EncodingPresetOption.Fast => "fast",
            EncodingPresetOption.Medium => "medium",
            EncodingPresetOption.Slow => "slow",
            _ => throw new ArgumentOutOfRangeException(nameof(preset), preset, "Unsupported encoding preset."),
        };
    }

    private static string GetVp9SpeedArguments(EncodingPresetOption preset)
    {
        return preset switch
        {
            EncodingPresetOption.Ultrafast => "-deadline realtime -cpu-used 5",
            EncodingPresetOption.Fast => "-deadline good -cpu-used 4",
            EncodingPresetOption.Medium => "-deadline good -cpu-used 2",
            EncodingPresetOption.Slow => "-deadline good -cpu-used 0",
            _ => throw new ArgumentOutOfRangeException(nameof(preset), preset, "Unsupported encoding preset."),
        };
    }
}
