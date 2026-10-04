using DinoSplicer.Api.Models;

using FFMpegCore;

namespace DinoSplicer.Api.Services;

public class FFProbeVideoMetadataExtractor : IVideoMetadataExtractor
{
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    private readonly Func<string, CancellationToken, Task<IMediaAnalysis>> _analyse;

    public FFProbeVideoMetadataExtractor()
        : this((path, token) => FFProbe.AnalyseAsync(path, cancellationToken: token))
    {
    }

    public FFProbeVideoMetadataExtractor(Func<string, CancellationToken, Task<IMediaAnalysis>> analyse)
    {
        _analyse = analyse;
    }

    public async Task<VideoMetadata> ExtractMetadata(
        string filePath,
        bool extractCodec = true,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default
    )
    {
        using CancellationTokenSource timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout ?? DefaultTimeout);

        IMediaAnalysis analysis;
        try
        {
            analysis = await _analyse(filePath, timeoutSource.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"FFProbe did not finish within {timeout ?? DefaultTimeout}.");
        }

        return Map(analysis, extractCodec);
    }

    public static VideoMetadata Map(IMediaAnalysis analysis, bool extractCodec)
    {
        VideoStream video = analysis.PrimaryVideoStream
            ?? throw new InvalidOperationException("The file does not contain a video stream.");

        return new VideoMetadata(
            analysis.Duration,
            new Resolution(video.Width, video.Height),
            extractCodec ? video.CodecName : null
        );
    }
}
