using DinoSplicer.Api.Models;

namespace DinoSplicer.Api.Services;

public interface IVideoCutter
{
    Task<CutResult> CutAsync(
        Guid jobId,
        string sourcePath,
        string outputDirectory,
        double startSeconds,
        double endSeconds,
        CutMode mode,
        CancellationToken cancellationToken = default
    );
}
