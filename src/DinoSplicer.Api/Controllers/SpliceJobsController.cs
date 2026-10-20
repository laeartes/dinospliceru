using DinoSplicer.Api.Data;
using DinoSplicer.Api.Models;
using DinoSplicer.Api.Services;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DinoSplicer.Api.Controllers;

[ApiController]
[Route("api/splice-jobs")]
public class SpliceJobsController(
    AppDbContext db,
    IOptions<VideoUploadOptions> uploadOptions,
    IWebHostEnvironment environment
) : ApiControllerBase
{
    private readonly VideoUploadOptions _options = uploadOptions.Value;

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SpliceJobResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        SpliceJob? job = await FindWithOutputsAsync(id, cancellationToken);
        if (job is null)
        {
            return SpliceJobNotFound(id);
        }

        return SpliceJobResponse.FromJob(job);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        // Outputs are not loaded: the database cascade removes their rows, the folder delete their files
        SpliceJob? job = await db.SpliceJobs.FindAsync([id], cancellationToken);
        if (job is null)
        {
            return SpliceJobNotFound(id);
        }

        // FFmpeg is still writing the segment files while the job is Processing
        if (job.Status == SpliceJobStatus.Processing)
        {
            return ApiErrorResult(
                StatusCodes.Status409Conflict,
                "The splice job cannot be deleted while it is being processed. Try again once it has finished.",
                ApiErrorCodes.SpliceJobProcessing
            );
        }

        // Same order as VideoController.Delete: if the folder delete fails afterwards we only leave orphan files
        db.SpliceJobs.Remove(job);
        await db.SaveChangesAsync(cancellationToken);

        // Removes the job's folder: every segment file, plus any file that has no output row
        string storageDirectory = Path.GetFullPath(_options.StoragePath, environment.ContentRootPath);
        string outputDirectory = SpliceProcessingWorker.GetOutputDirectory(storageDirectory, job.Id);
        if (Directory.Exists(outputDirectory))
        {
            Directory.Delete(outputDirectory, recursive: true);
        }

        return NoContent();
    }

    private Task<SpliceJob?> FindWithOutputsAsync(Guid id, CancellationToken cancellationToken)
    {
        return db.SpliceJobs
            .Include(j => j.Outputs)
            .FirstOrDefaultAsync(j => j.Id == id, cancellationToken);
    }

    private ObjectResult SpliceJobNotFound(Guid id)
    {
        return ApiErrorResult(
            StatusCodes.Status404NotFound,
            $"No splice job with id '{id}' exists.",
            ApiErrorCodes.SpliceJobNotFound
        );
    }
}
