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
[Route("api/compression-jobs")]
public class CompressionJobsController(
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

    [HttpGet("{id:guid}/download")]
    public async Task<IActionResult> Download(Guid id, CancellationToken cancellationToken)
    {
        CompressionJob job = await db.CompressionJobs
            .AsNoTracking()
            .FirstOrDefaultAsync(j => j.Id == id, cancellationToken)
            ?? throw CreateCompressionJobNotFoundException(id);

        if (job.Status != CompressionJobStatus.Ready)
        {
            throw new ResourceConflictException(
                $"The compression job is {job.Status}; it must be Ready before its output can be downloaded.",
                ApiErrorCodes.CompressionJobNotReady
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

        // The output extension (.mp4 or .webm) tells the content type
        if (!contentTypeProvider.TryGetContentType(job.OutputFileName, out string? contentType))
        {
            contentType = MediaTypeNames.Application.Octet;
        }

        return PhysicalFile(filePath, contentType, job.OutputFileName, enableRangeProcessing: true);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        CompressionJob job = await db.CompressionJobs.FindAsync([id], cancellationToken)
            ?? throw CreateCompressionJobNotFoundException(id);

        // FFmpeg is still writing the output file while the job is Processing
        if (job.Status == CompressionJobStatus.Processing)
        {
            throw new ResourceConflictException(
                "The compression job cannot be deleted while it is being processed. Try again once it has finished.",
                ApiErrorCodes.CompressionJobProcessing
            );
        }

        // Row first: if the folder delete fails afterwards we only leave orphan files
        db.CompressionJobs.Remove(job);
        await db.SaveChangesAsync(cancellationToken);
        progressTracker.Remove(job.Id);

        string storageDirectory = Path.GetFullPath(_options.StoragePath, environment.ContentRootPath);
        string outputDirectory = CompressionJob.GetOutputDirectory(storageDirectory, job.Id);
        await fileSystem.DeleteDirectoryRecursiveAsync(outputDirectory, CancellationToken.None);

        return NoContent();
    }

    private static ResourceNotFoundException CreateCompressionJobNotFoundException(Guid id)
    {
        return new ResourceNotFoundException(
            $"No compression job with id '{id}' exists.",
            ApiErrorCodes.CompressionJobNotFound
        );
    }

    private static ResourceNotFoundException CreateOutputFileMissingException()
    {
        return new ResourceNotFoundException(
            "The file for this compression job no longer exists on the server.",
            ApiErrorCodes.CompressionOutputFileMissing
        );
    }
}
