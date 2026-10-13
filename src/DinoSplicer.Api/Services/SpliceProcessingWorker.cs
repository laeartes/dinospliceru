using DinoSplicer.Api.Data;
using DinoSplicer.Api.Models;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DinoSplicer.Api.Services;

public class SpliceProcessingWorker(
    SpliceJobQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<SpliceProcessingWorker> logger
) : BackgroundService
{
    // Segments are stored under <storage>/splices/<job id>/
    public const string OutputDirectoryName = "splices";

    public static string GetOutputDirectory(string storageDirectory, Guid jobId)
    {
        return Path.Combine(storageDirectory, OutputDirectoryName, jobId.ToString());
    }

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
                logger.LogError(ex, "Unexpected error while processing splice job {JobId}", jobId);
            }
        }
    }

    public async Task RecoverPendingJobsAsync(CancellationToken cancellationToken)
    {
        try
        {
            using IServiceScope scope = scopeFactory.CreateScope();
            AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            List<Guid> pendingIds = await db.SpliceJobs
                .Where(j => j.Status == SpliceJobStatus.Pending || j.Status == SpliceJobStatus.Processing)
                .OrderBy(j => j.CreatedAt)
                .Select(j => j.Id)
                .ToListAsync(cancellationToken);

            foreach (Guid id in pendingIds)
            {
                await queue.EnqueueAsync(id, cancellationToken);
            }

            if (pendingIds.Count > 0)
            {
                logger.LogInformation("Re-queued {Count} unfinished splice job(s) on startup", pendingIds.Count);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Host is stopping before recovery completed
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to recover pending splice jobs on startup");
        }
    }

    public async Task ProcessAsync(Guid jobId, CancellationToken cancellationToken)
    {
        using IServiceScope scope = scopeFactory.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        VideoUploadOptions options = scope.ServiceProvider.GetRequiredService<IOptions<VideoUploadOptions>>().Value;
        IWebHostEnvironment environment = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();

        SpliceJob? job = await db.SpliceJobs
            .Include(j => j.SourceVideo)
            .FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken);
        if (job is null || job.SourceVideo is null)
        {
            logger.LogWarning("Splice job {JobId} no longer exists, skipping", jobId);
            return;
        }

        Video video = job.SourceVideo;

        if (job.Status is SpliceJobStatus.Ready or SpliceJobStatus.Failed)
        {
            logger.LogInformation("Splice job {JobId} is already {Status}, skipping", jobId, job.Status);
            return;
        }

        // A job that is already Processing was interrupted by a shutdown, so its partial output gets replaced
        bool resuming = job.Status == SpliceJobStatus.Processing;
        job.Status = SpliceJobStatus.Processing;
        await db.SaveChangesAsync(cancellationToken);

        string storageDirectory = Path.GetFullPath(options.StoragePath, environment.ContentRootPath);
        string outputDirectory = GetOutputDirectory(storageDirectory, job.Id);

        try
        {
            string sourcePath = Path.Combine(storageDirectory, video.FileName);

            
            IReadOnlyList<SpliceSegment> segments = resuming
                ? await VideoSplicer.Splice(sourcePath, outputDirectory, job.Mode, job.SegmentValue, overwriteExisting: true, cancellationToken: cancellationToken)
                : await VideoSplicer.Splice(sourcePath, outputDirectory, job.Mode, job.SegmentValue, cancellationToken: cancellationToken);

            string baseName = Path.GetFileNameWithoutExtension(video.OriginalFileName);
            foreach (SpliceSegment segment in segments)
            {
                string segmentFileName = Path.GetFileName(segment.FilePath);

                // Added through the DbSet: SpliceOutput sets its own Id, so EF would treat one that is only
                // reachable through job.Outputs as an existing row and update it instead of inserting it
                db.SpliceOutputs.Add(new SpliceOutput
                {
                    SpliceJobId = job.Id,
                    FileName = $"{baseName}_part{segment.Index:D2}{Path.GetExtension(segmentFileName)}",
                    StoragePath = Path.Combine(OutputDirectoryName, job.Id.ToString(), segmentFileName),
                    SegmentIndex = segment.Index,
                    DurationSeconds = segment.DurationSeconds,
                    SizeBytes = segment.SizeBytes,
                });
            }

            job.Status = SpliceJobStatus.Ready;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The job stays Processing so it is resumed on the next startup
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Splicing failed for job {JobId}", jobId);
            job.Status = SpliceJobStatus.Failed;
            job.ErrorMessage = ex is SpliceException ? ex.Message : "The video could not be spliced.";

            if (Directory.Exists(outputDirectory))
            {
                Directory.Delete(outputDirectory, recursive: true);
            }
        }

        await db.SaveChangesAsync(CancellationToken.None);
    }
}
