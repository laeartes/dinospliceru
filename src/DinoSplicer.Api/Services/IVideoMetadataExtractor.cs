using DinoSplicer.Api.Models;

namespace DinoSplicer.Api.Services;

public interface IVideoMetadataExtractor
{
    Task<VideoMetadata> ExtractMetadata(
        string filePath,
        bool extractCodec = true,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default
    );
}
