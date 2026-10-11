using System.Globalization;

using DinoSplicer.Api.Models;

namespace DinoSplicer.Api.Services;

public static class SpliceRequestValidator
{
    public const double MinSegmentSeconds = 1;
    public const double MinSegmentBytes = 1024 * 1024;

    // Returns an error message for the value, or null when it is acceptable for this video
    public static string? Validate(SpliceMode mode, double value, Video video)
    {
        return mode switch
        {
            SpliceMode.ByLength => CheckRange(value, MinSegmentSeconds, video.DurationSeconds, "seconds"),
            SpliceMode.BySize => CheckRange(value, MinSegmentBytes, video.SizeBytes, "bytes"),
            _ => $"Unsupported mode '{mode}'."
        };
    }

    private static string? CheckRange(double value, double minimum, double? maximum, string unit)
    {
        // The invariant culture keeps messages free of locale quirks such as decimal commas.
        if (value < minimum)
        {
            return string.Create(CultureInfo.InvariantCulture, $"Value must be at least {minimum} {unit}.");
        }

        if (maximum is double limit && value > limit)
        {
            return string.Create(CultureInfo.InvariantCulture, $"Value must not exceed the video's {limit} {unit}.");
        }

        return null;
    }
}
