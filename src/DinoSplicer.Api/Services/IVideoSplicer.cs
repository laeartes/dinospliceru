using DinoSplicer.Api.Models;

namespace DinoSplicer.Api.Services;

public interface IVideoSplicer
{
    Task<IReadOnlyList<SpliceSegment>> Splice(
        string sourcePath,
        string outputDirectory,
        SpliceMode mode,
        double value,
        bool overwriteExisting = false,
        CancellationToken cancellationToken = default
    );
}
