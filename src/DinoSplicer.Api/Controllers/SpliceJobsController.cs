using DinoSplicer.Api.Data;
using DinoSplicer.Api.Models;

using Microsoft.AspNetCore.Mvc;

namespace DinoSplicer.Api.Controllers;

[ApiController]
[Route("api/splice-jobs")]
public class SpliceJobsController(AppDbContext db) : ApiControllerBase
{
    // Status only for now; the output segments are added to this response in #33
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SpliceJobResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        SpliceJob? job = await db.SpliceJobs.FindAsync([id], cancellationToken);
        if (job is null)
        {
            return ApiErrorResult(
                StatusCodes.Status404NotFound,
                $"No splice job with id '{id}' exists.",
                ApiErrorCodes.SpliceJobNotFound
            );
        }

        return SpliceJobResponse.FromJob(job);
    }
}
