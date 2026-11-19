using DinoSplicer.Api.Models;

namespace DinoSplicer.Api.Services;

public interface IFFMpegRunner
{
    Task RunCutAsync(
        string sourcePath,
        string outputPath,
        double startSeconds,
        double endSeconds,
        CutMode mode,
        Action<TimeSpan>? onProgress = null,
        CancellationToken cancellationToken = default
    );
}
