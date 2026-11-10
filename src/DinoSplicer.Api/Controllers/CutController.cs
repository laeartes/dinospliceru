using DinoSplicer.Api.Data;
using DinoSplicer.Api.Exceptions;
using DinoSplicer.Api.Models;
using DinoSplicer.Api.Services;

using Microsoft.AspNetCore.Mvc;

namespace DinoSplicer.Api.Controllers;

[ApiController]
[Route("api/videos/{id:guid}/cut")]
public class CutController(
    AppDbContext db,
    ICutRequestValidator validator,
    IBackgroundJobQueue<CutJob> cutQueue
) : ApiControllerBase
{
    [HttpPost]
    public async Task<IActionResult> CreateCut(Guid id, CutRequest request, CancellationToken cancellationToken)
    {
        Video? video = await db.Videos.FindAsync([id], cancellationToken);
        if (video is null)
        {
            throw new ResourceNotFoundException($"No video with id '{id}' exists.", ApiErrorCodes.VideoNotFound);
        }

        if (video.Status != VideoStatus.Ready)
        {
            throw new ResourceConflictException(
                $"The video is {video.Status}; it must be Ready before it can be cut.",
                ApiErrorCodes.VideoNotReady
            );
        }

        // Needs the video's duration, so it runs after the existence and readiness checks
        validator.Validate(request, video);

        CutJob job = new()
        {
            SourceVideoId = video.Id,
            StartSeconds = request.StartSeconds,
            EndSeconds = request.EndSeconds,
            Mode = request.Mode,
        };
        db.CutJobs.Add(job);
        await db.SaveChangesAsync(cancellationToken);

        // The row is already committed, so enqueueing must not be abandoned if the client disconnects
        await cutQueue.EnqueueAsync(job.Id, CancellationToken.None);

        return Accepted(CutJobResponse.FromJob(job));
    }
}
