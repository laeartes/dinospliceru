namespace DinoSplicer.Api.Models;

public record VideoUploadConfigResponse(
    long MaxFileSizeBytes,
    string[] AllowedExtensions,
    string[] AllowedContentTypes
)
{
    // StoragePath is left out on purpose: it is a server-side path the client has no use for
    public static VideoUploadConfigResponse FromOptions(VideoUploadOptions options)
    {
        return new VideoUploadConfigResponse(
            options.MaxFileSizeBytes,
            options.AllowedExtensions,
            options.AllowedContentTypes
        );
    }
}
