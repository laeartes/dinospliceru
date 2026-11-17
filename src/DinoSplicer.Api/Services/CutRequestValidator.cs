using System.Globalization;

using DinoSplicer.Api.Exceptions;
using DinoSplicer.Api.Models;

namespace DinoSplicer.Api.Services;

public class CutRequestValidator : ICutRequestValidator
{
    public const double MinClipSeconds = 0.1;

    // One millisecond. Absorbs floating-point noise (10.1 - 10.0 comes out slightly below 0.1) and
    // sub-millisecond rounding when a client echoes back a duration it read from the video metadata.
    public const double ToleranceSeconds = 0.001;

    public void Validate(CutRequest request, Video video)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(video);

        if (!Enum.IsDefined(request.Mode))
        {
            throw new DomainValidationException($"Unsupported cut mode '{request.Mode}'.", ApiErrorCodes.InvalidCutMode);
        }

        // NaN compares false against everything, so non-finite values must be rejected before any range check
        if (!double.IsFinite(request.StartSeconds) || !double.IsFinite(request.EndSeconds))
        {
            throw InvalidRange("StartSeconds and EndSeconds must be finite numbers.");
        }

        if (request.StartSeconds < 0)
        {
            throw InvalidRange("StartSeconds must not be negative.");
        }

        if (request.EndSeconds <= request.StartSeconds)
        {
            throw InvalidRange("EndSeconds must be greater than StartSeconds.");
        }

        if (request.EndSeconds - request.StartSeconds < MinClipSeconds - ToleranceSeconds)
        {
            // The invariant culture keeps messages free of locale quirks such as decimal commas.
            throw InvalidRange(string.Create(
                CultureInfo.InvariantCulture,
                $"The clip must be at least {MinClipSeconds} seconds long."));
        }

        // The duration is unknown until metadata extraction finishes, so the upper bound is skipped without it
        if (video.DurationSeconds is double duration && request.EndSeconds > duration + ToleranceSeconds)
        {
            throw InvalidRange(string.Create(
                CultureInfo.InvariantCulture,
                $"EndSeconds must not exceed the video's duration of {duration} seconds."));
        }
    }

    private static DomainValidationException InvalidRange(string message)
    {
        return new DomainValidationException(message, ApiErrorCodes.InvalidCutRange);
    }
}
