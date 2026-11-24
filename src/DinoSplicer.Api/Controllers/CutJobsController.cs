using System.Net.Mime;

using DinoSplicer.Api.Data;
using DinoSplicer.Api.Exceptions;
using DinoSplicer.Api.Models;
using DinoSplicer.Api.Services;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DinoSplicer.Api.Controllers;

[ApiController]
[Route("api/cut-jobs")]
public class CutJobsController(
    AppDbContext db,
    IJobProgressTracker progressTracker,
    IContentTypeProvider contentTypeProvider,
    IFileSystem fileSystem,
    IOptions<VideoUploadOptions> uploadOptions,
    IWebHostEnvironment environment
) : ApiControllerBase
{
    private readonly VideoUploadOptions _options = uploadOptions.Value;

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CutJobResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        CutJob job = await db.CutJobs
            .AsNoTracking()
            .FirstOrDefaultAsync(j => j.Id == id, cancellationToken)
            ?? throw CreateCutJobNotFoundException(id);

        int? progressPercent = job.Status switch
        {
            CutJobStatus.Pending => 0,
            CutJobStatus.Processing => progressTracker.GetProgress(job.Id) ?? 0,
            CutJobStatus.Ready => 100,
            _ => null,
        };

        return CutJobResponse.FromJob(job, progressPercent);
    }

    [HttpGet("{id:guid}/download")]
    public async Task<IActionResult> Download(Guid id, CancellationToken cancellationToken)
    {
        CutJob job = await db.CutJobs
            .AsNoTracking()
            .FirstOrDefaultAsync(j => j.Id == id, cancellationToken)
            ?? throw CreateCutJobNotFoundException(id);

        if (job.Status != CutJobStatus.Ready)
        {
            throw new ResourceConflictException(
                $"The cut job is {job.Status}; it must be Ready before its output can be downloaded.",
                ApiErrorCodes.CutJobNotReady
            );
        }

        if (string.IsNullOrWhiteSpace(job.OutputStoragePath) || string.IsNullOrWhiteSpace(job.OutputFileName))
        {
            throw CreateOutputFileMissingException();
        }

        string storageDirectory = Path.GetFullPath(_options.StoragePath, environment.ContentRootPath);
        string filePath = Path.Combine(storageDirectory, job.OutputStoragePath);
        if (!await fileSystem.FileExistsAsync(filePath, cancellationToken))
        {
            throw CreateOutputFileMissingException();
        }

        if (!contentTypeProvider.TryGetContentType(job.OutputFileName, out string? contentType))
        {
            contentType = MediaTypeNames.Application.Octet;
        }

        return PhysicalFile(filePath, contentType, job.OutputFileName, enableRangeProcessing: true);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        CutJob job = await db.CutJobs.FindAsync([id], cancellationToken)
            ?? throw CreateCutJobNotFoundException(id);

        if (job.Status == CutJobStatus.Processing)
        {
            throw new ResourceConflictException(
                "The cut job cannot be deleted while it is being processed. Try again once it has finished.",
                ApiErrorCodes.CutJobProcessing
            );
        }

        db.CutJobs.Remove(job);
        await db.SaveChangesAsync(cancellationToken);
        progressTracker.Remove(job.Id);

        string storageDirectory = Path.GetFullPath(_options.StoragePath, environment.ContentRootPath);
        string outputDirectory = CutJob.GetOutputDirectory(storageDirectory, job.Id);
        await fileSystem.DeleteDirectoryRecursiveAsync(outputDirectory, CancellationToken.None);

        return NoContent();
    }

    private static ResourceNotFoundException CreateCutJobNotFoundException(Guid id)
    {
        return new ResourceNotFoundException(
            $"No cut job with id '{id}' exists.",
            ApiErrorCodes.CutJobNotFound
        );
    }

    private static ResourceNotFoundException CreateOutputFileMissingException()
    {
        return new ResourceNotFoundException(
            "The file for this cut job no longer exists on the server.",
            ApiErrorCodes.CutOutputFileMissing
        );
    }
}
