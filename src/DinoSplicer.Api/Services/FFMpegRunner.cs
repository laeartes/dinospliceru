using System.Globalization;

using DinoSplicer.Api.Models;

using FFMpegCore;

namespace DinoSplicer.Api.Services;

public class FFMpegRunner : IFFMpegRunner
{
    public Task RunCutAsync(
        string sourcePath,
        string outputPath,
        double startSeconds,
        double endSeconds,
        CutMode mode,
        Action<TimeSpan>? onProgress = null,
        CancellationToken cancellationToken = default
    )
    {
        string startFormatted = FormatSeconds(startSeconds);
        string endFormatted = FormatSeconds(endSeconds);

        return mode switch
        {
            CutMode.FastCopy => FFMpegArguments
                .FromFileInput(sourcePath, verifyExists: false, addArguments: options => options
                    .WithCustomArgument($"-ss {startFormatted} -to {endFormatted}"))
                .OutputToFile(outputPath, overwrite: true, addArguments: options => options
                    .WithCustomArgument("-map 0:v:0 -map 0:a? -c copy -avoid_negative_ts make_zero"))
                .CancellableThrough(cancellationToken)
                .ProcessAsynchronously(),

            CutMode.FrameAccurate => BuildFrameAccurateArguments(
                sourcePath,
                outputPath,
                startFormatted,
                endFormatted,
                onProgress,
                cancellationToken
            ),

            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported cut mode."),
        };
    }

    private static Task BuildFrameAccurateArguments(
        string sourcePath,
        string outputPath,
        string startFormatted,
        string endFormatted,
        Action<TimeSpan>? onProgress,
        CancellationToken cancellationToken
    )
    {
        FFMpegArgumentProcessor processor = FFMpegArguments
            .FromFileInput(sourcePath, verifyExists: false, addArguments: options => options
                .WithCustomArgument($"-ss {startFormatted} -to {endFormatted}"))
            .OutputToFile(outputPath, overwrite: true, addArguments: options => options
                .WithCustomArgument(GetFrameAccurateOutputArguments(outputPath)));

        if (onProgress is not null)
        {
            processor = processor.NotifyOnProgress(onProgress);
        }

        return processor
            .CancellableThrough(cancellationToken)
            .ProcessAsynchronously();
    }

    public static string GetFrameAccurateOutputArguments(string outputPath)
    {
        string extension = Path.GetExtension(outputPath).ToLowerInvariant();
        return extension switch
        {
            ".webm" => "-map 0:v:0 -map 0:a? -c:v libvpx-vp9 -crf 30 -b:v 0 -deadline realtime -cpu-used 4 -c:a libopus",
            ".mp4" or ".m4v" or ".mov" or ".mkv" => "-map 0:v:0 -map 0:a? -vf \"scale=trunc(iw/2)*2:trunc(ih/2)*2\" -c:v libx264 -preset fast -crf 18 -pix_fmt yuv420p -c:a aac",
            _ => "-map 0:v:0 -map 0:a?",
        };
    }

    public static string FormatSeconds(double seconds)
    {
        return seconds.ToString("0.######", CultureInfo.InvariantCulture);
    }
}