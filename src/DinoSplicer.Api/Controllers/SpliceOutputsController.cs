using System.Net.Mime;

using DinoSplicer.Api.Data;
using DinoSplicer.Api.Models;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DinoSplicer.Api.Controllers;

[ApiController]
[Route("api/splice-outputs")]
public class SpliceOutputsController(
    AppDbContext db,
    IOptions<VideoUploadOptions> uploadOptions,
    IWebHostEnvironment environment
) : ApiControllerBase
{
    private static readonly FileExtensionContentTypeProvider _contentTypes = new();

    private readonly VideoUploadOptions _options = uploadOptions.Value;

    [HttpGet("{id:guid}/download")]
    public async Task<IActionResult> Download(Guid id, CancellationToken cancellationToken)
    {
        SpliceOutput? output = await db.SpliceOutputs
            .AsNoTracking()
            .Include(o => o.SpliceJob)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
        if (output is null)
        {
            return ApiErrorResult(
                StatusCodes.Status404NotFound,
                $"No splice output with id '{id}' exists.",
                ApiErrorCodes.SpliceOutputNotFound
            );
        }

        SpliceJob job = output.SpliceJob!;
        if (job.Status != SpliceJobStatus.Ready)
        {
            return ApiErrorResult(
                StatusCodes.Status409Conflict,
                $"The splice job is {job.Status}; it must be Ready before its outputs can be downloaded.",
                ApiErrorCodes.SpliceJobNotReady
            );
        }

        string storageDirectory = Path.GetFullPath(_options.StoragePath, environment.ContentRootPath);
        string filePath = Path.Combine(storageDirectory, output.StoragePath);
        if (!System.IO.File.Exists(filePath))
        {
            return ApiErrorResult(
                StatusCodes.Status404NotFound,
                "The file for this splice output no longer exists on the server.",
                ApiErrorCodes.SpliceOutputFileMissing
            );
        }

        // Segments keep the source video's extension, so the extension tells the content type
        if (!_contentTypes.TryGetContentType(output.FileName, out string? contentType))
        {
            contentType = MediaTypeNames.Application.Octet;
        }

        return PhysicalFile(filePath, contentType, output.FileName, enableRangeProcessing: true);
    }
}
