using System.Globalization;

using FFMpegCore;

namespace DinoSplicer.Api.Tests;

// Videos with sound, generated with FFmpeg once per test run.
// The checked-in sample is only one second long and silent, which is too short to splice.
public static class SampleVideo
{
    public const int DurationSeconds = 10;

    // A keyframe every second
    private static readonly Lazy<Task<string>> _generated = new(() =>
        GenerateAsync("splice-sample.mp4", DurationSeconds, "-g 30 -shortest"));

    // Frequent keyframes let size-based segments fill up close to the limit.
    private static readonly Lazy<Task<string>> _lowBitrate = new(() =>
        GenerateAsync("splice-sample-low-bitrate.mp4", 30, "-c:v libx264 -b:v 60k -g 10 -c:a aac -b:a 32k -shortest"));

    public static Task<string> GetPathAsync()
    {
        return _generated.Value;
    }

    public static Task<string> GetLowBitratePathAsync()
    {
        return _lowBitrate.Value;
    }

    private static async Task<string> GenerateAsync(string fileName, int durationSeconds, string encodeArguments)
    {
        string directory = Path.Combine(Path.GetTempPath(), "dinosplicer-tests");
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, fileName);
        string duration = durationSeconds.ToString(CultureInfo.InvariantCulture);

        await FFMpegArguments
            .FromFileInput($"testsrc2=size=320x240:rate=30:duration={duration}", verifyExists: false, options => options.ForceFormat("lavfi"))
            .AddFileInput($"sine=frequency=440:duration={duration}", verifyExists: false, options => options.ForceFormat("lavfi"))
            .OutputToFile(path, addArguments: options => options.WithCustomArgument(encodeArguments))
            .ProcessAsynchronously();

        return path;
    }
}
