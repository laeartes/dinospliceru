using DinoSplicer.Api.Data;
using DinoSplicer.Api.Exceptions;
using DinoSplicer.Api.Models;
using DinoSplicer.Api.Services;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DinoSplicer.Api.Controllers;

[ApiController]
[Route("api/compression-jobs")]
public class CompressionJobsController(
    AppDbContext db,
    IJobProgressTracker progressTracker
) : ApiControllerBase
{
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CompressionJobResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        CompressionJob job = await db.CompressionJobs
            .AsNoTracking()
            .FirstOrDefaultAsync(j => j.Id == id, cancellationToken)
            ?? throw CreateCompressionJobNotFoundException(id);

        int? progressPercent = job.Status switch
        {
            CompressionJobStatus.Pending => 0,
            CompressionJobStatus.Processing => progressTracker.GetProgress(job.Id) ?? 0,
            CompressionJobStatus.Ready => 100,
            _ => null,
        };

        return CompressionJobResponse.FromJob(job, progressPercent);
    }

    private static ResourceNotFoundException CreateCompressionJobNotFoundException(Guid id)
    {
        return new ResourceNotFoundException(
            $"No compression job with id '{id}' exists.",
            ApiErrorCodes.CompressionJobNotFound
        );
    }
}