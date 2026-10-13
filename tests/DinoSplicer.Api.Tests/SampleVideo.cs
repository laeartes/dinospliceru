using System.Globalization;

using FFMpegCore;

namespace DinoSplicer.Api.Tests;

// A video with sound and a keyframe every second, generated with FFmpeg once per test run.
// The checked-in sample is only one second long and silent, which is too short to splice.
public static class SampleVideo
{
    public const int DurationSeconds = 10;

    private static readonly Lazy<Task<string>> _generated = new(GenerateAsync);

    public static Task<string> GetPathAsync()
    {
        return _generated.Value;
    }

    private static async Task<string> GenerateAsync()
    {
        string directory = Path.Combine(Path.GetTempPath(), "dinosplicer-tests");
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "splice-sample.mp4");
        string duration = DurationSeconds.ToString(CultureInfo.InvariantCulture);

        await FFMpegArguments
            .FromFileInput($"testsrc2=size=320x240:rate=30:duration={duration}", verifyExists: false, options => options.ForceFormat("lavfi"))
            .AddFileInput($"sine=frequency=440:duration={duration}", verifyExists: false, options => options.ForceFormat("lavfi"))
            .OutputToFile(path, addArguments: options => options.WithCustomArgument("-g 30 -shortest"))
            .ProcessAsynchronously();

        return path;
    }
}
