using DinoSplicer.Api.Data;
using DinoSplicer.Api.Models;
using DinoSplicer.Api.Services;

using Microsoft.AspNetCore.Mvc;

namespace DinoSplicer.Api.Controllers;

[ApiController]
[Route("api/videos/{id:guid}/splice")]
public class SpliceController(AppDbContext db, SpliceJobQueue spliceQueue) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> CreateSplice(Guid id, SpliceRequest request, CancellationToken cancellationToken)
    {
        Video? video = await db.Videos.FindAsync([id], cancellationToken);
        if (video is null)
        {
            return Problem(
                title: "Video not found",
                detail: $"No video with id '{id}' exists.",
                statusCode: StatusCodes.Status404NotFound
            );
        }

        if (video.Status != VideoStatus.Ready)
        {
            return Problem(
                title: "Video is not ready",
                detail: $"The video is {video.Status}; it must be Ready before it can be spliced.",
                statusCode: StatusCodes.Status409Conflict
            );
        }

        string? valueError = SpliceRequestValidator.Validate(request.Mode, request.Value, video);
        if (valueError is not null)
        {
            ModelState.AddModelError(nameof(SpliceRequest.Value), valueError);
            return ValidationProblem(ModelState);
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
