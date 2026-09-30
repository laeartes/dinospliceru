using System.Diagnostics.CodeAnalysis;

using DinoSplicer.Api.Models;

namespace DinoSplicer.Api.Extensions;

public static class FormFileExtensions
{
    private const int MaxFileNameLength = 255;

    public static bool IsValidVideoFile(
        this IFormFile file,
        IReadOnlyCollection<string> allowedExtensions,
        IReadOnlyCollection<string> allowedContentTypes,
        long maxSizeBytes,
        out VideoFileError reason,
        [NotNullWhen(false)] out string? error
    )

    {
        if (file.Length == 0)
        {
            reason = VideoFileError.Empty;
            error = "The uploaded file is empty.";
            return false;
        }

        if (file.Length > maxSizeBytes)
        {
            reason = VideoFileError.TooLarge;
            error = $"The file is {file.Length} bytes, which exceeds the maximum allowed size of {maxSizeBytes} bytes.";
            return false;
        }

        if (Path.GetFileName(file.FileName).Length > MaxFileNameLength)
        {
            reason = VideoFileError.FileNameTooLong;
            error = $"The file name must be at most {MaxFileNameLength} characters long.";
            return false;
        }

        string extension = Path.GetExtension(file.FileName);
        if (!allowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            reason = VideoFileError.ExtensionNotAllowed;
            error = $"The file extension '{extension}' is not allowed. Allowed extensions: {string.Join(", ", allowedExtensions)}.";
            return false;
        }

        if (!allowedContentTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
        {
            reason = VideoFileError.ContentTypeNotAllowed;
            error = $"The content type '{file.ContentType}' is not allowed Allowed types: {string.Join(", ", allowedContentTypes)}";
            return false;
        }

        reason = VideoFileError.None;
        error = null;
        return true;
    }
}
