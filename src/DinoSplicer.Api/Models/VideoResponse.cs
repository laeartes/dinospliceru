namespace DinoSplicer.Api.Models;

public record VideoResponse(
    Guid Id,
    string FileName,
    string ContentType,
    long SizeBytes,
    double? DurationSeconds,
    string Status,
    DateTime CreatedAt
)
{
    // FileName is the user's original name, matching VideoUploadResponse.
    // The generated on-disk name stays internal.
    public static VideoResponse FromVideo(Video video)
    {
        return new VideoResponse(
            video.Id,
            video.OriginalFileName,
            video.ContentType,
            video.SizeBytes,
            video.DurationSeconds,
            video.Status.ToString(),
            video.CreatedAt
        );
    }
}
