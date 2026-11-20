using DinoSplicer.Api.Exceptions;
using DinoSplicer.Api.Models;

namespace DinoSplicer.Api.Services;

public class FFMpegVideoCutter(
    IFFMpegRunner ffmpegRunner,
    IVideoMetadataExtractor metadataExtractor,
    IJobProgressTracker progressTracker,
    IFileSystem fileSystem,
    ILogger<FFMpegVideoCutter> logger
) : IVideoCutter
{
    private const string OutputFileBaseName = "clip";

    private readonly IFFMpegRunner _ffmpegRunner = ffmpegRunner ?? throw new ArgumentNullException(nameof(ffmpegRunner));
    private readonly IVideoMetadataExtractor _metadataExtractor = metadataExtractor ?? throw new ArgumentNullException(nameof(metadataExtractor));
    private readonly IJobProgressTracker _progressTracker = progressTracker ?? throw new ArgumentNullException(nameof(progressTracker));
    private readonly IFileSystem _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
    private readonly ILogger<FFMpegVideoCutter> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public async Task<CutResult> CutAsync(
        Guid jobId,
        string sourcePath,
        string outputDirectory,
        double startSeconds,
        double endSeconds,
        CutMode mode,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

        if (!double.IsFinite(startSeconds) || startSeconds < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(startSeconds), startSeconds, "StartSeconds must be a non-negative finite number.");
        }

        if (!double.IsFinite(endSeconds) || endSeconds <= startSeconds)
        {
            throw new ArgumentOutOfRangeException(nameof(endSeconds), endSeconds, "EndSeconds must be finite and greater than StartSeconds.");
        }

        if (!Enum.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported cut mode.");
        }

        string extension = Path.GetExtension(sourcePath);
        if (string.IsNullOrWhiteSpace(extension))
        {
            throw new MediaProcessingException("The source video file must have a valid container extension.");
        }

        bool directoryCreated = await _fileSystem.CreateDirectoryIfNotExistsAsync(outputDirectory, cancellationToken);
        string outputPath = Path.Combine(outputDirectory, $"{OutputFileBaseName}{extension}");
        double targetDurationSeconds = endSeconds - startSeconds;

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

        Action<TimeSpan>? onProgress = null;
        if (mode == CutMode.FrameAccurate)
        {
            ReportMonotonicProgress(0);
            onProgress = elapsed =>
            {
                int percentage = (int)Math.Round((elapsed.TotalSeconds / targetDurationSeconds) * 100.0);
                ReportMonotonicProgress(percentage);
            };
        }

        try
        {
            await _ffmpegRunner.RunCutAsync(
                sourcePath,
                outputPath,
                startSeconds,
                endSeconds,
                mode,
                onProgress,
                cancellationToken
            );

            VideoMetadata metadata = await _metadataExtractor.ExtractMetadata(
                filePath: outputPath,
                extractCodec: false,
                cancellationToken: cancellationToken
            );

            if (metadata.Duration <= TimeSpan.Zero)
            {
                throw new MediaProcessingException("The cut video clip has zero duration.");
            }

            long sizeBytes = await _fileSystem.GetFileLengthAsync(outputPath, cancellationToken);
            if (sizeBytes <= 0)
            {
                throw new MediaProcessingException("The cut video clip is empty.");
            }

            if (mode == CutMode.FrameAccurate)
            {
                ReportMonotonicProgress(100);
            }

            return new CutResult(outputPath, metadata.Duration.TotalSeconds, sizeBytes);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Video cut cancelled for job {JobId}", jobId);
            await TryCleanupAsync(outputPath, outputDirectory, directoryCreated);
            throw;
        }
        catch (MediaProcessingException ex)
        {
            _logger.LogWarning(ex, "Video cut validation failed for job {JobId}", jobId);
            await TryCleanupAsync(outputPath, outputDirectory, directoryCreated);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "FFmpeg video cut failed for job {JobId}", jobId);
            await TryCleanupAsync(outputPath, outputDirectory, directoryCreated);
            throw new MediaProcessingException("The video clip could not be cut.", innerException: ex);
        }
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
            _logger.LogWarning(cleanupEx, "Failed to clean up partial cut output at {OutputPath}", outputPath);
        }
    }
}
