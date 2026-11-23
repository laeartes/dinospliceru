using DinoSplicer.Api.Data;
using DinoSplicer.Api.Exceptions;
using DinoSplicer.Api.Models;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DinoSplicer.Api.Services;

public class CompressionProcessingWorker(
    IBackgroundJobQueue<CompressionJob> queue,
    IVideoAccessCoordinator accessCoordinator,
    IJobProgressTracker progressTracker,
    IServiceScopeFactory scopeFactory,
    ILogger<CompressionProcessingWorker> logger
) : BackgroundService
{
    public const string GenericFailureMessage = "The video could not be compressed.";
    public const string SourceBeingDeletedMessage = "The source video is being deleted.";
    private const int MaxOutputFileNameLength = 255;
    private const int MaxErrorMessageLength = 500;
    private const string OutputFileNameSuffix = "_compressed";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RecoverPendingJobsAsync(stoppingToken);

        await foreach (Guid jobId in queue.DequeueAllAsync(stoppingToken))
        {
            try
            {
                await ProcessAsync(jobId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error while processing compression job {JobId}", jobId);
            }
        }
    }

    public Task<int> RecoverPendingJobsAsync(CancellationToken cancellationToken)
    {
        return queue.RecoverPendingAsync(
            scopeFactory,
            logger,
            jobDescription: "compression job(s)",
            isPending: j => j.Status == CompressionJobStatus.Pending || j.Status == CompressionJobStatus.Processing,
            orderBy: j => j.CreatedAt,
            idSelector: j => j.Id,
            cancellationToken: cancellationToken
        );
    }

    public async Task ProcessAsync(Guid jobId, CancellationToken cancellationToken)
    {
        using IServiceScope scope = scopeFactory.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        IVideoCompressor compressor = scope.ServiceProvider.GetRequiredService<IVideoCompressor>();
        IFileSystem fileSystem = scope.ServiceProvider.GetRequiredService<IFileSystem>();
        VideoUploadOptions options = scope.ServiceProvider.GetRequiredService<IOptions<VideoUploadOptions>>().Value;
        IWebHostEnvironment environment = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();

        CompressionJob? job = await db.CompressionJobs
            .Include(j => j.SourceVideo)
            .FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken);
        if (job is null || job.SourceVideo is null)
        {
            logger.LogWarning("Compression job {JobId} no longer exists, skipping", jobId);
            return;
        }

        if (job.Status is CompressionJobStatus.Ready or CompressionJobStatus.Failed)
        {
            logger.LogInformation("Compression job {JobId} is already {Status}, skipping", jobId, job.Status);
            return;
        }

        Video video = job.SourceVideo;

        using IDisposable? lease = accessCoordinator.TryAcquireProcessingLease(video.Id);
        if (lease is null)
        {
            logger.LogInformation("Source video {VideoId} for compression job {JobId} is being deleted, skipping", video.Id, jobId);

            job.Status = CompressionJobStatus.Failed;
            job.ErrorMessage = SourceBeingDeletedMessage;
            await db.SaveChangesAsync(CancellationToken.None);
            return;
        }

        // Deleting a video cascade-deletes its jobs, so there is no job left to mark as failed
        if (!await db.Videos.AnyAsync(v => v.Id == video.Id, cancellationToken))
        {
            logger.LogWarning("Source video {VideoId} for compression job {JobId} was deleted before compression started, skipping", video.Id, jobId);
            return;
        }

        job.Status = CompressionJobStatus.Processing;
        await db.SaveChangesAsync(cancellationToken);

        string storageDirectory = Path.GetFullPath(options.StoragePath, environment.ContentRootPath);
        string sourcePath = Path.Combine(storageDirectory, video.FileName);
        string outputDirectory = CompressionJob.GetOutputDirectory(storageDirectory, job.Id);

        CompressionRequest request = new()
        {
            Format = job.Format,
            VideoCodec = job.VideoCodec,
            Crf = job.Crf,
            TargetWidth = job.TargetWidth,
            TargetHeight = job.TargetHeight,
            AudioBitrateKbps = job.AudioBitrateKbps,
            Preset = job.Preset,
        };

        TimeSpan? sourceDuration = video.DurationSeconds is double duration and > 0
            ? TimeSpan.FromSeconds(duration)
            : null;

        try
        {
            CompressionResult result = await compressor.CompressAsync(
                job.Id,
                sourcePath,
                outputDirectory,
                request,
                sourceDuration,
                cancellationToken
            );

            // The compressor returns an absolute path, the database keeps it relative to the storage folder
            string outputFile = Path.GetFileName(result.FilePath);
            job.OutputFileName = BuildOutputFileName(video.OriginalFileName, Path.GetExtension(outputFile));
            job.OutputStoragePath = Path.Combine(CompressionJob.OutputDirectoryName, job.Id.ToString(), outputFile);
            job.OutputDurationSeconds = result.DurationSeconds;
            job.OutputSizeBytes = result.SizeBytes;
            job.Status = CompressionJobStatus.Ready;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            progressTracker.Remove(job.Id);
            throw;
        }
        catch (MediaProcessingException ex)
        {
            logger.LogWarning(ex, "Compression failed for job {JobId}", jobId);
            await MarkFailedAsync(job, ex.Message, outputDirectory, fileSystem);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error while compressing job {JobId}", jobId);
            await MarkFailedAsync(job, GenericFailureMessage, outputDirectory, fileSystem);
        }

        progressTracker.Remove(job.Id);

        // If this save fails the job stays Processing and RecoverPendingJobsAsync re-queues it on the next startup
        await db.SaveChangesAsync(CancellationToken.None);
    }

    public static string BuildOutputFileName(string originalFileName, string outputExtension)
    {
        string baseName = Path.GetFileNameWithoutExtension(originalFileName);
        int maxBaseLength = MaxOutputFileNameLength - OutputFileNameSuffix.Length - outputExtension.Length;
        if (baseName.Length > maxBaseLength)
        {
            baseName = baseName[..maxBaseLength];
        }

        return $"{baseName}{OutputFileNameSuffix}{outputExtension}";
    }

    private async Task MarkFailedAsync(CompressionJob job, string errorMessage, string outputDirectory, IFileSystem fileSystem)
    {
        job.Status = CompressionJobStatus.Failed;
        job.ErrorMessage = errorMessage.Length <= MaxErrorMessageLength
            ? errorMessage
            : errorMessage[..MaxErrorMessageLength];

        try
        {
            await fileSystem.DeleteDirectoryRecursiveAsync(outputDirectory, CancellationToken.None);
        }
        catch (Exception cleanupEx)
        {
            logger.LogWarning(cleanupEx, "Failed to delete partial compression output for job {JobId}", job.Id);
        }
    }
}
