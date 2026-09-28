using System.Diagnostics.CodeAnalysis;

namespace DinoSplicer.Api.Extensions;

public static class FormFileExtensions
{
    private const int MaxFileNameLength = 255;

    public static bool IsValidVideoFile(
        this IFormFile file,
        IReadOnlyCollection<string> allowedExtensions,
        IReadOnlyCollection<string> allowedContentTypes,
        long maxSizeBytes,
        [NotNullWhen(false)] out string? error
    )

    {
        if (file.Length == 0)
        {
            error = "The uploaded file is empty.";
            return false;
        }

        if (file.Length > maxSizeBytes)
        {
            error = $"The file is {file.Length} bytes, which exceeds the maximum allowed size.";
            return false;
        }

        if (Path.GetFileName(file.FileName).Length > MaxFileNameLength)
        {
            error = $"The file name must be at most {MaxFileNameLength} characters long.";
            return false;
        }

        string extensions = Path.GetExtension(file.FileName);
        if (!allowedExtensions.Contains(extensions, StringComparer.OrdinalIgnoreCase))
        {
            error = $"The file extension '{extension}' is not allowed. Allowed extensions: {string.Join(", ", allowedExtensions)}.";
            return false;
        }

        if (!allowedContentTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
        {
            error = $"The content type '{file.ContentType}' is not allowed Allowed types: {string.Join(", ", allowedContentTypes)}"
            return false;
        }

        error = null;
        return true;
    }
}