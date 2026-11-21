using DinoSplicer.Api.Models;

namespace DinoSplicer.Api.Services;

public interface IVideoCompressor
{
    Task<CompressionResult> CompressAsync(
        Guid jobId,
        string sourcePath,
        string outputDirectory,
        CompressionRequest request,
        TimeSpan? sourceDuration = null,
        CancellationToken cancellationToken = default
    );
}
