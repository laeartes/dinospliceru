using DinoSplicer.Api.Data;
using DinoSplicer.Api.Exceptions;
using DinoSplicer.Api.Models;
using DinoSplicer.Api.Services;

using Microsoft.AspNetCore.Mvc;

namespace DinoSplicer.Api.Controllers;

[ApiController]
[Route("api/videos/{id:guid}/compress")]
public class CompressionController(
    AppDbContext db,
    ICompressionRequestValidator validator,
    IBackgroundJobQueue<CompressionJob> compressionQueue
) : ApiControllerBase
{
    [HttpPost]
    public async Task<IActionResult> CreateCompression(Guid id, CompressionRequest request, CancellationToken cancellationToken)
    {
        Video? video = await db.Videos.FindAsync([id], cancellationToken);
        if (video is null)
        {
            throw new ResourceNotFoundException($"No video with id '{id}' exists.", ApiErrorCodes.VideoNotFound);
        }

        if (video.Status != VideoStatus.Ready)
        {
            throw new ResourceConflictException(
                $"The video is {video.Status}; it must be Ready before it can be compressed.",
                ApiErrorCodes.VideoNotReady
            );
        }

        validator.Validate(request, video);

        CompressionJob job = new()
        {
            SourceVideoId = video.Id,
            Format = request.Format,
            VideoCodec = request.VideoCodec,
            Crf = request.Crf,
            TargetWidth = request.TargetWidth,
            TargetHeight = request.TargetHeight,
            AudioBitrateKbps = request.AudioBitrateKbps,
            TargetSizeBytes = request.TargetSizeBytes,
            Preset = request.Preset,
        };
        db.CompressionJobs.Add(job);
        await db.SaveChangesAsync(cancellationToken);

        await compressionQueue.EnqueueAsync(job.Id, CancellationToken.None);

        return Accepted(CompressionJobResponse.FromJob(job));
    }
}
