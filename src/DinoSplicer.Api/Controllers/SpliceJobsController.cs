using DinoSplicer.Api.Data;
using DinoSplicer.Api.Models;

using Microsoft.AspNetCore.Mvc;

namespace DinoSplicer.Api.Controllers;

[ApiController]
[Route("api/splice-jobs")]
public class SpliceJobsController(AppDbContext db) : ControllerBase
{
    // Status only for now; the output segments are added to this response in #33
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SpliceJobResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        SpliceJob? job = await db.SpliceJobs.FindAsync([id], cancellationToken);
        if (job is null)
        {
            return Problem(
                title: "Splice job not found",
                detail: $"No splice job with id '{id}' exists.",
                statusCode: StatusCodes.Status404NotFound
            );
        }

        return SpliceJobResponse.FromJob(job);
    }
}
