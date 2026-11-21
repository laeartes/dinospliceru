using DinoSplicer.Api.Data;
using DinoSplicer.Api.Exceptions;
using DinoSplicer.Api.Models;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DinoSplicer.Api.Services;

public class CutProcessingWorker (
    IBackgroundJobQueue<CutJob> queue,
    IVideoAccessCoordinator accessCoordinator,
    IJobProgressTracker progressTracker,
    IServiceScopeFactory scopeFactory,
    ILogger<CutProcessingWorker> logger
) : BackgroundService
{
    public const string GenericFailureMessage = "The video could not be cut.";
    public const string SourceBeingDeletedMessage = "The source video is being deleted.";

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
                logger.LogError(ex, "Unexpected error while processing cut job {JobId}", jobId);
            }
        }
    }

    public Task<int> RecoverPendingJobsAsync(CancellationToken cancellationToken)
    {
        return queue.RecoverPendingAsync(
            scopeFactory,
            logger,
            jobDescription: "cut job(s)",
            isPending: j => j.Status == CutJobStatus.Pending || j.Status == CutJobStatus.Processing,
            orderBy: j => j.CreatedAt,
            idSelector: j => j.Id,
            cancellationToken: cancellationToken
        );
    }

    public async Task ProcessAsync(Guid jobId, CancellationToken cancellationToken)
    {
        using IServiceScope scope = scopeFactory.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        IVideoCutter cutter = scope.ServiceProvider.GetRequiredService<IVideoCutter>();
        IFileSystem fileSystem = scope.ServiceProvider.GetRequiredService<IFileSystem>();
        VideoUploadOptions options = scope.ServiceProvider.GetRequiredService<IOptions<VideoUploadOptions>>().Value;
        IWebHostEnvironment environment = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();

        CutJob? job = await db.CutJobs
            .Include(j => j.SourceVideo)
            .FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken);
        if (job is null || job.SourceVideo is null)
        {
            logger.LogWarning("Cut job {JobId} no longer exists, skipping", jobId);
            return;
        }

        if (job.Status is CutJobStatus.Ready or CutJobStatus.Failed)
        {
            logger.LogInformation("Cut job {JobId} is already {Status}, skipping", jobId, job.Status);
            return;
        }

        Video video = job.SourceVideo;

        using IDisposable? lease = accessCoordinator.TryAcquireProcessingLease(video.Id);
        if (lease is null)
        {
            logger.LogInformation("Source video {VideoId} for cut job {JobId} is being deleted, skipping", video.Id, jobId);

            job.Status = CutJobStatus.Failed;
            job.ErrorMessage = SourceBeingDeletedMessage;
            await db.SaveChangesAsync(CancellationToken.None);
            return;
        }

        if (!await db.Videos.AnyAsync(v => v.Id == video.Id, cancellationToken))
        {
            logger.LogWarning("Source video {VideoId} for cut job {JobId} was deleted before cutting started, skipping", video.Id, jobId);
            return;
        }
        
        job.Status = CutJobStatus.Processing;
        await db.SaveChangesAsync(cancellationToken);

        string storageDirectory = Path.GetFullPath(options.StoragePath, environment.ContentRootPath);
        string sourcePath = Path.Combine(storageDirectory, video.FileName);
        string outputDirectory = CutJob.GetOutputDirectory(storageDirectory, job.Id);

        try
        {
            CutResult result = await cutter.CutAsync(
                job.Id,
                sourcePath,
                outputDirectory,
                job.StartSeconds,
                job.EndSeconds,
                job.Mode,
                cancellationToken
            );

            // The cutter returns an absolute path, the database keeps it relative to the storage folder
            string outputFile = Path.GetFileName(result.FilePath);
            job.OutputFileName = $"{Path.GetFileNameWithoutExtension(video.OriginalFileName)}_cut{Path.GetExtension(outputFile)}";
            job.OutputStoragePath = Path.Combine(CutJob.OutputDirectoryName, job.Id.ToString(), outputFile);
            job.OutputDurationSeconds = result.DurationSeconds;
            job.OutputSizeBytes = result.SizeBytes;
            job.Status = CutJobStatus.Ready;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            progressTracker.Remove(job.Id);
            throw;
        }
        catch (MediaProcessingException ex)
        {
            logger.LogWarning(ex, "Cutting failed for job {JobId}", jobId);
            await MarkFailedAsync(job, ex.Message, outputDirectory, fileSystem);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error while cutting job {JobId}", jobId);
            await MarkFailedAsync(job, GenericFailureMessage, outputDirectory, fileSystem);
        }

        progressTracker.Remove(job.Id);
        await db.SaveChangesAsync(CancellationToken.None);
    }

    private async Task MarkFailedAsync(CutJob job, string errorMessage, string outputDirectory, IFileSystem fileSystem)
    {
        job.Status = CutJobStatus.Failed;
        job.ErrorMessage = errorMessage;

        try
        {
            await fileSystem.DeleteDirectoryRecursiveAsync(outputDirectory, CancellationToken.None);
        }
        catch (Exception cleanupEx)
        {
            logger.LogWarning(cleanupEx, "Failed to delete partial cut output for job {JobId}", job.Id);
        }
    }
}