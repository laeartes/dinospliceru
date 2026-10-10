using DinoSplicer.Api.Data;
using DinoSplicer.Api.Extensions;
using DinoSplicer.Api.Models;
using DinoSplicer.Api.Services;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace DinoSplicer.Api.Controllers;

[ApiController]
[Route("api/videos")]
public class VideoController(
    AppDbContext db,
    IOptions<VideoUploadOptions> uploadOptions,
    IWebHostEnvironment environment,
    VideoProcessingQueue processingQueue
) : ControllerBase

{
    private readonly VideoUploadOptions _options = uploadOptions.Value;

    [HttpPost]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Upload(CancellationToken cancellationToken)
    {
        IFormCollection form;
        try
        {
            form = await Request.ReadFormAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is InvalidDataException or BadHttpRequestException)
        {
            int limitStatusCode = ex is BadHttpRequestException badRequest
                ? badRequest.StatusCode
                : StatusCodes.Status413PayloadTooLarge;

            return Problem(
                title: "Upload rejected",
                detail: $"The request is too large or malformed. Maximum file size is {_options.MaxFileSizeBytes} bytes.",
                statusCode: limitStatusCode
            );
        }

        if (form.Files.Count != 1)
        {
            return Problem(
                title: "Invalid upload",
                detail: $"Exactly one file must be uploaded per request, but {form.Files.Count} were received.",
                statusCode: StatusCodes.Status400BadRequest
            );
        }

        IFormFile file = form.Files[0];

        if (!file.IsValidVideoFile(
                _options.AllowedExtensions,
                _options.AllowedContentTypes,
                _options.MaxFileSizeBytes,
                out VideoFileError reason,
                out string? error
        ))

        {
            int statusCode = reason switch
            {
                VideoFileError.TooLarge => StatusCodes.Status413PayloadTooLarge,
                VideoFileError.ExtensionNotAllowed or VideoFileError.ContentTypeNotAllowed => StatusCodes.Status415UnsupportedMediaType,
                _ => StatusCodes.Status400BadRequest,
            };

            return Problem(title: "Invalid video file", detail: error, statusCode: statusCode);
        }

        string storageDirectory = Path.GetFullPath(_options.StoragePath, environment.ContentRootPath);
        Directory.CreateDirectory(storageDirectory);

        Video video = new()
        {
            OriginalFileName = Path.GetFileName(file.FileName),
            ContentType = file.ContentType,
            SizeBytes = file.Length,
            Status = VideoStatus.Uploaded,
        };
        video.FileName = $"{video.Id}{Path.GetExtension(file.FileName).ToLowerInvariant()}";
        string fullPath = Path.Combine(storageDirectory, video.FileName);

        try
        {
            await using (Stream source = file.OpenReadStream())
            await using (FileStream destination = new(
                fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, bufferSize: 81920, useAsync: true
            ))

            {
                await source.CopyToAsync(destination, cancellationToken);
            }

            db.Videos.Add(video);
            await db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            System.IO.File.Delete(fullPath);
            throw;
        }

        await processingQueue.EnqueueAsync(video.Id, CancellationToken.None);

        VideoUploadResponse response = new(video.Id, video.OriginalFileName, video.SizeBytes, video.Status.ToString());
        return Created($"/api/videos/{video.Id}", response);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<VideoResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        Video? video = await db.Videos.FindAsync([id], cancellationToken);
        if (video is null)
        {
            return VideoNotFound(id);
        }

        return VideoResponse.FromVideo(video);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        Video? video = await db.Videos.FindAsync([id], cancellationToken);
        if (video is null)
        {
            return VideoNotFound(id);
        }

        // FFmpeg may have the file open while processing, so deleting now could fail or break the job
        if (video.Status == VideoStatus.Processing)
        {
            return Problem(
                title: "Video is being processed",
                detail: "The video cannot be deleted while it is being processed. Try again once processing has finished.",
                statusCode: StatusCodes.Status409Conflict
            );
        }

        // Remove the DB row first: if the file delete then fails we are left with an orphan file,
        // which is harmless, instead of a row pointing at a file that no longer exists
        db.Videos.Remove(video);
        await db.SaveChangesAsync(cancellationToken);

        // Same path as built in Upload, keep the two in sync
        string storageDirectory = Path.GetFullPath(_options.StoragePath, environment.ContentRootPath);
        string fullPath = Path.Combine(storageDirectory, video.FileName);

        if (System.IO.File.Exists(fullPath))
        {
            System.IO.File.Delete(fullPath);
        }

        return NoContent();
    }

    private ObjectResult VideoNotFound(Guid id)
    {
        return Problem(
            title: "Video not found",
            detail: $"No video with id '{id}' exists.",
            statusCode: StatusCodes.Status404NotFound
        );
    }
}
