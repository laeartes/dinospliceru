using DinoSplicer.Api.Data;
using DinoSplicer.Api.Extensions;
using DinoSplicer.Api.Models;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace DinoSplicer.Api.Controllers;

[ApiController]
[Route("api/videos")]
public class VideoController(
    AppDbContext db,
    IOptions<VideoUploadOptions> uploadOptions,
    IWebHostEnvironment environment
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
        video.StoragePath = Path.Combine(storageDirectory, video.FileName);

        try
        {
            await using (Stream source = file.OpenReadStream())
            await using (FileStream destination = new(
                video.StoragePath, FileMode.CreateNew, FileAccess.Write, FileShare.None, bufferSize: 81920, useAsync: true
            ))

            {
                await source.CopyToAsync(destination, cancellationToken);
            }

            db.Videos.Add(video);
            await db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            System.IO.File.Delete(video.StoragePath);
            throw;
        }

        VideoUploadResponse response = new(video.Id, video.OriginalFileName, video.SizeBytes, video.Status.ToString());
        return Created($"/api/videos/{video.Id}", response);
    }
}
