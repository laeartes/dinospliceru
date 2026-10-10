using DinoSplicer.Api.Data;
using DinoSplicer.Api.Models;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DinoSplicer.Api.Services;

public class VideoProcessingWorker(
    VideoProcessingQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<VideoProcessingWorker> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RecoverPendingVideosAsync(stoppingToken);

        await foreach (Guid videoId in queue.DequeueAllAsync(stoppingToken))
        {
            try
            {
                await ProcessAsync(videoId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error while processing video {VideoId}", videoId);
            }
        }
    }

    public async Task RecoverPendingVideosAsync(CancellationToken cancellationToken)
    {
        try
        {
            using IServiceScope scope = scopeFactory.CreateScope();
            AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            List<Video> stuckProcessing = await db.Videos
                .Where(v => v.Status == VideoStatus.Processing)
                .ToListAsync(cancellationToken);

            foreach (Video video in stuckProcessing)
            {
                video.Status = VideoStatus.Failed;
                logger.LogWarning("Video {VideoId} was in Processing during startup; marked as Failed", video.Id);
            }

            if (stuckProcessing.Count > 0)
            {
                await db.SaveChangesAsync(cancellationToken);
            }

            List<Guid> pendingUploads = await db.Videos
                .Where(v => v.Status == VideoStatus.Uploaded)
                .Select(v => v.Id)
                .ToListAsync(cancellationToken);

            foreach (Guid id in pendingUploads)
            {
                await queue.EnqueueAsync(id, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Host is stopping before recovery completed
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to recover pending videos on startup");
        }
    }

    public async Task ProcessAsync(Guid videoId, CancellationToken cancellationToken)
    {
        using IServiceScope scope = scopeFactory.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        IVideoMetadataExtractor extractor = scope.ServiceProvider.GetRequiredService<IVideoMetadataExtractor>();
        VideoUploadOptions options = scope.ServiceProvider.GetRequiredService<IOptions<VideoUploadOptions>>().Value;
        IWebHostEnvironment environment = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();

        Video? video = await db.Videos.FindAsync([videoId], cancellationToken);
        if (video is null)
        {
            logger.LogWarning("Video {VideoId} no longer exists, skipping metadata extraction", videoId);
            return;
        }

        video.Status = VideoStatus.Processing;
        await db.SaveChangesAsync(cancellationToken);

        string filePath = Path.Combine(Path.GetFullPath(options.StoragePath, environment.ContentRootPath), video.FileName);

        try
        {
            VideoMetadata metadata = await extractor.ExtractMetadata(
                filePath: filePath,
                extractCodec: true,
                timeout: TimeSpan.FromSeconds(options.MetadataTimeoutSeconds),
                cancellationToken: cancellationToken
            );

            video.DurationSeconds = metadata.Duration.TotalSeconds;
            video.Resolution = metadata.Resolution;
            video.Codec = metadata.Codec;
            video.Status = VideoStatus.Ready;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            video.Status = VideoStatus.Failed;
            await db.SaveChangesAsync(CancellationToken.None);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Metadata extraction failed for video {VideoId}", videoId);
            video.Status = VideoStatus.Failed;
        }

        await db.SaveChangesAsync(CancellationToken.None);
    }
}
