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
        SpliceJob? job = await FindWithOutputsAsync(id, cancellationToken);
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

        // Copied before saving: EF detaches the deleted outputs and can drop them from job.Outputs
        List<SpliceOutput> outputs = [.. job.Outputs];

        // Same order as VideoController.Delete: if a file delete fails afterwards we only leave an orphan file
        db.SpliceJobs.Remove(job);
        await db.SaveChangesAsync(cancellationToken);

        string storageDirectory = Path.GetFullPath(_options.StoragePath, environment.ContentRootPath);
        foreach (SpliceOutput output in outputs)
        {
            string filePath = Path.Combine(storageDirectory, output.StoragePath);
            if (System.IO.File.Exists(filePath))
            {
                System.IO.File.Delete(filePath);
            }
        }

        // Only the segment files are written to this folder, so it is empty by now
        string outputDirectory = SpliceProcessingWorker.GetOutputDirectory(storageDirectory, job.Id);
        if (Directory.Exists(outputDirectory))
        {
            Directory.Delete(outputDirectory);
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
