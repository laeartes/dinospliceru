using DinoSplicer.Api.Data;
using DinoSplicer.Api.Models;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DinoSplicer.Api.Controllers;

[ApiController]
[Route("api/splice-jobs")]
public class SpliceJobsController(AppDbContext db) : ApiControllerBase
{
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SpliceJobResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        SpliceJob? job = await db.SpliceJobs
            .Include(j => j.Outputs)
            .FirstOrDefaultAsync(j => j.Id == id, cancellationToken);
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
