using DinoSplicer.Api.Data;
using DinoSplicer.Api.Extensions;
using DinoSplicer.Api.Models;
using DinoSplicer.Api.Services;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DinoSplicer.Api.Controllers;

[ApiController]
[Route("api/videos")]
public class VideoController(
    AppDbContext db,
    IOptions<VideoUploadOptions> uploadOptions,
    IWebHostEnvironment environment,
    VideoProcessingQueue processingQueue
) : ApiControllerBase

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

            string limitCode = limitStatusCode == StatusCodes.Status413PayloadTooLarge
                ? ApiErrorCodes.FileTooLarge
                : ApiErrorCodes.InvalidUpload;

            return ApiErrorResult(
                limitStatusCode,
                $"The request is too large or malformed. Maximum file size is {_options.MaxFileSizeBytes} bytes.",
                limitCode
            );
        }

        if (form.Files.Count != 1)
        {
            return ApiErrorResult(
                StatusCodes.Status400BadRequest,
                $"Exactly one file must be uploaded per request, but {form.Files.Count} were received.",
                ApiErrorCodes.InvalidFileCount
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

            return ApiErrorResult(statusCode, error, ApiErrorCodes.ForVideoFileError(reason));
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
        catch (Exception ex) when (ex is Microsoft.EntityFrameworkCore.DbUpdateException
                                or Npgsql.NpgsqlException
                                or System.Net.Sockets.SocketException
                                or System.IO.IOException)
        {
            System.IO.File.Delete(fullPath);
            return ApiErrorResult(
                StatusCodes.Status500InternalServerError,
                "Couldn't connect to database or save video record. Please ensure the database is running and migrations are applied.",
                ApiErrorCodes.DatabaseError
            );
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
            return ApiErrorResult(
                StatusCodes.Status409Conflict,
                "The video cannot be deleted while it is being processed. Try again once processing has finished.",
                ApiErrorCodes.VideoProcessing
            );
        }

        List<SpliceJob> spliceJobs = await db.SpliceJobs
            .Where(j => j.SourceVideoId == id)
            .ToListAsync(cancellationToken);

        // Same reason as above: FFmpeg reads the video while splicing it
        if (spliceJobs.Any(j => j.Status == SpliceJobStatus.Processing))
        {
            return ApiErrorResult(
                StatusCodes.Status409Conflict,
                "The video cannot be deleted while it is being spliced. Try again once splicing has finished.",
                ApiErrorCodes.VideoSplicing
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

        // The database deletes the splice jobs together with the video, their segment files are removed here
        foreach (SpliceJob job in spliceJobs)
        {
            string outputDirectory = SpliceProcessingWorker.GetOutputDirectory(storageDirectory, job.Id);
            if (Directory.Exists(outputDirectory))
            {
                Directory.Delete(outputDirectory, recursive: true);
            }
        }

        return NoContent();
    }

    private ObjectResult VideoNotFound(Guid id)
    {
        return ApiErrorResult(
            StatusCodes.Status404NotFound,
            $"No video with id '{id}' exists.",
            ApiErrorCodes.VideoNotFound
        );
    }
}
