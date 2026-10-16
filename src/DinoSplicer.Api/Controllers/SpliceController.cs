using DinoSplicer.Api.Data;
using DinoSplicer.Api.Models;
using DinoSplicer.Api.Services;

using Microsoft.AspNetCore.Mvc;

namespace DinoSplicer.Api.Controllers;

[ApiController]
[Route("api/videos/{id:guid}/splice")]
public class SpliceController(AppDbContext db, SpliceJobQueue spliceQueue) : ApiControllerBase
{
    [HttpPost]
    public async Task<IActionResult> CreateSplice(Guid id, SpliceRequest request, CancellationToken cancellationToken)
    {
        Video? video = await db.Videos.FindAsync([id], cancellationToken);
        if (video is null)
        {
            return ApiErrorResult(
                StatusCodes.Status404NotFound,
                $"No video with id '{id}' exists.",
                ApiErrorCodes.VideoNotFound
            );
        }

        if (video.Status != VideoStatus.Ready)
        {
            return ApiErrorResult(
                StatusCodes.Status409Conflict,
                $"The video is {video.Status}; it must be Ready before it can be spliced.",
                ApiErrorCodes.VideoNotReady
            );
        }

        string? valueError = SpliceRequestValidator.Validate(request.Mode, request.Value, video);
        if (valueError is not null)
        {
            return ApiErrorResult(StatusCodes.Status400BadRequest, valueError, ApiErrorCodes.InvalidSpliceValue);
        }

        SpliceJob job = new()
        {
            SourceVideoId = video.Id,
            Mode = request.Mode,
            SegmentValue = request.Value,
        };
        db.SpliceJobs.Add(job);
        await db.SaveChangesAsync(cancellationToken);

        await spliceQueue.EnqueueAsync(job.Id, CancellationToken.None);

        return Accepted(SpliceJobResponse.FromJob(job));
    }
}
