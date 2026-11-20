using DinoSplicer.Api.Exceptions;
using DinoSplicer.Api.Models;

namespace DinoSplicer.Api.Services;

public class CompressionRequestValidator : ICompressionRequestValidator
{
    public const int MaxH264Crf = 51;

    public void Validate(CompressionRequest request, Video video)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(video);

        ValidateEnums(request);
        ValidateContainerCodecCompatibility(request.Format, request.VideoCodec);
        ValidateCrf(request.VideoCodec, request.Crf);
        ValidateResolution(request.TargetWidth, request.TargetHeight, video.Resolution);
        ValidateAudioBitrate(request.AudioBitrateKbps);
    }

    private static void ValidateEnums(CompressionRequest request)
    {
        if (!Enum.IsDefined(request.Format))
        {
            throw new DomainValidationException(
                $"Unsupported container format '{request.Format}'.",
                ApiErrorCodes.InvalidCompressionFormat);
        }

        if (!Enum.IsDefined(request.VideoCodec))
        {
            throw new DomainValidationException(
                $"Unsupported video codec '{request.VideoCodec}'.",
                ApiErrorCodes.InvalidCompressionCodec);
        }

        if (!Enum.IsDefined(request.Preset))
        {
            throw new DomainValidationException(
                $"Unsupported encoding preset '{request.Preset}'.",
                ApiErrorCodes.InvalidCompressionPreset);
        }
    }

    private static void ValidateContainerCodecCompatibility(OutputContainerFormat format, VideoCodecOption codec)
    {
        bool isCompatible = (format, codec) switch
        {
            (OutputContainerFormat.Mp4, VideoCodecOption.H264) => true,
            (OutputContainerFormat.Webm, VideoCodecOption.Vp9) => true,
            _ => false,
        };

        if (!isCompatible)
        {
            throw new DomainValidationException(
                $"Container format '{format}' is not compatible with video codec '{codec}'.",
                ApiErrorCodes.IncompatibleContainerCodec);
        }
    }

    private static void ValidateCrf(VideoCodecOption codec, int crf)
    {
        int maxCrf = codec == VideoCodecOption.H264 ? MaxH264Crf : CompressionJob.MaxCrf;

        if (crf < CompressionJob.MinCrf || crf > maxCrf)
        {
            throw new DomainValidationException(
                $"CRF must be between {CompressionJob.MinCrf} and {maxCrf} for codec '{codec}'.",
                ApiErrorCodes.InvalidCompressionCrf);
        }
    }

    private static void ValidateResolution(int? targetWidth, int? targetHeight, Resolution? sourceResolution)
    {
        if (targetWidth is null && targetHeight is null)
        {
            return;
        }

        if (targetWidth is null || targetHeight is null)
        {
            throw new DomainValidationException(
                "TargetWidth and TargetHeight must both be specified or both be omitted.",
                ApiErrorCodes.InvalidCompressionResolution);
        }

        ValidateDimension(nameof(CompressionRequest.TargetWidth), targetWidth.Value, sourceResolution?.Width);
        ValidateDimension(nameof(CompressionRequest.TargetHeight), targetHeight.Value, sourceResolution?.Height);
    }

    private static void ValidateDimension(string name, int value, int? maxSourceValue)
    {
        if (value <= 0)
        {
            throw new DomainValidationException(
                $"{name} must be a positive integer.",
                ApiErrorCodes.InvalidCompressionResolution);
        }

        if (value % 2 != 0)
        {
            throw new DomainValidationException(
                $"{name} must be an even number (divisible by 2) for yuv420p compatibility.",
                ApiErrorCodes.InvalidCompressionResolution);
        }

        // Source resolution is unknown until metadata extraction finishes, so the upper bound is skipped without it
        if (maxSourceValue is int max && value > max)
        {
            throw new DomainValidationException(
                $"{name} ({value}) must not exceed the source video dimension ({max}).",
                ApiErrorCodes.InvalidCompressionResolution);
        }
    }

    private static void ValidateAudioBitrate(int? audioBitrateKbps)
    {
        if (audioBitrateKbps is null)
        {
            return;
        }

        if (audioBitrateKbps < CompressionJob.MinAudioBitrateKbps ||
            audioBitrateKbps > CompressionJob.MaxAudioBitrateKbps)
        {
            throw new DomainValidationException(
                $"AudioBitrateKbps must be between {CompressionJob.MinAudioBitrateKbps} and {CompressionJob.MaxAudioBitrateKbps} kbps.",
                ApiErrorCodes.InvalidCompressionAudioBitrate);
        }
    }
}
