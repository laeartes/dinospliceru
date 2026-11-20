using System.Net;

using DinoSplicer.Api.Exceptions;
using DinoSplicer.Api.Models;
using DinoSplicer.Api.Services;

using FluentAssertions;

using Microsoft.Extensions.DependencyInjection;

namespace DinoSplicer.Api.Tests.Services;

public class CompressionRequestValidatorTests
{
    private readonly CompressionRequestValidator _validator = new();

    private static Video CreateVideo(int? width = 1920, int? height = 1080) => new()
    {
        FileName = "source.mp4",
        OriginalFileName = "source.mp4",
        ContentType = "video/mp4",
        SizeBytes = 10_485_760,
        DurationSeconds = 120,
        Resolution = width is int w && height is int h ? new Resolution(w, h) : null,
        Status = VideoStatus.Ready
    };

    private static CompressionRequest CreateRequest(
        OutputContainerFormat format = OutputContainerFormat.Mp4,
        VideoCodecOption codec = VideoCodecOption.H264,
        int crf = CompressionRequest.DefaultCrf,
        int? targetWidth = null,
        int? targetHeight = null,
        int? audioBitrateKbps = null,
        EncodingPresetOption preset = EncodingPresetOption.Medium) => new()
        {
            Format = format,
            VideoCodec = codec,
            Crf = crf,
            TargetWidth = targetWidth,
            TargetHeight = targetHeight,
            AudioBitrateKbps = audioBitrateKbps,
            Preset = preset
        };

    private void ShouldReject(CompressionRequest request, Video video, string expectedCode)
    {
        Action act = () => _validator.Validate(request, video);

        DomainValidationException exception = act.Should().Throw<DomainValidationException>().Which;
        exception.ErrorCode.Should().Be(expectedCode);
        exception.StatusCode.Should().Be((int)HttpStatusCode.BadRequest);
        exception.Message.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Validate_DefaultMp4H264WithOriginalResolution_DoesNotThrow()
    {
        Action act = () => _validator.Validate(CreateRequest(), CreateVideo());

        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(OutputContainerFormat.Mp4, VideoCodecOption.H264, 0)]
    [InlineData(OutputContainerFormat.Mp4, VideoCodecOption.H264, 28)]
    [InlineData(OutputContainerFormat.Mp4, VideoCodecOption.H264, 51)]
    [InlineData(OutputContainerFormat.Webm, VideoCodecOption.Vp9, 0)]
    [InlineData(OutputContainerFormat.Webm, VideoCodecOption.Vp9, 31)]
    [InlineData(OutputContainerFormat.Webm, VideoCodecOption.Vp9, 63)]
    public void Validate_ValidFormatCodecAndCrf_DoesNotThrow(
        OutputContainerFormat format,
        VideoCodecOption codec,
        int crf)
    {
        CompressionRequest request = CreateRequest(format: format, codec: codec, crf: crf);

        Action act = () => _validator.Validate(request, CreateVideo());

        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(1280, 720)]   // downscaled even resolution
    [InlineData(1920, 1080)]  // exact source resolution
    [InlineData(2, 2)]        // minimum positive even resolution
    public void Validate_ValidDownscaledResolution_DoesNotThrow(int targetWidth, int targetHeight)
    {
        CompressionRequest request = CreateRequest(targetWidth: targetWidth, targetHeight: targetHeight);

        Action act = () => _validator.Validate(request, CreateVideo(1920, 1080));

        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(null)]
    [InlineData(32)]
    [InlineData(128)]
    [InlineData(512)]
    public void Validate_ValidAudioBitrate_DoesNotThrow(int? audioBitrateKbps)
    {
        CompressionRequest request = CreateRequest(audioBitrateKbps: audioBitrateKbps);

        Action act = () => _validator.Validate(request, CreateVideo());

        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_UnknownSourceResolution_SkipsUpperBoundCheck()
    {
        CompressionRequest request = CreateRequest(targetWidth: 3840, targetHeight: 2160);

        Action act = () => _validator.Validate(request, CreateVideo(width: null, height: null));

        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(OutputContainerFormat.Mp4, VideoCodecOption.Vp9)]
    [InlineData(OutputContainerFormat.Webm, VideoCodecOption.H264)]
    public void Validate_IncompatibleFormatAndCodec_ThrowsIncompatibleContainerCodec(
        OutputContainerFormat format,
        VideoCodecOption codec)
    {
        CompressionRequest request = CreateRequest(format: format, codec: codec);

        ShouldReject(request, CreateVideo(), ApiErrorCodes.IncompatibleContainerCodec);
    }

    [Theory]
    [InlineData(OutputContainerFormat.Mp4, VideoCodecOption.H264, -1)]
    [InlineData(OutputContainerFormat.Mp4, VideoCodecOption.H264, 52)]
    [InlineData(OutputContainerFormat.Mp4, VideoCodecOption.H264, 63)]
    [InlineData(OutputContainerFormat.Webm, VideoCodecOption.Vp9, -1)]
    [InlineData(OutputContainerFormat.Webm, VideoCodecOption.Vp9, 64)]
    public void Validate_CrfOutOfRange_ThrowsInvalidCompressionCrf(
        OutputContainerFormat format,
        VideoCodecOption codec,
        int crf)
    {
        CompressionRequest request = CreateRequest(format: format, codec: codec, crf: crf);

        ShouldReject(request, CreateVideo(), ApiErrorCodes.InvalidCompressionCrf);
    }

    [Theory]
    [InlineData(1281, 720)]   // odd width
    [InlineData(1280, 721)]   // odd height
    [InlineData(0, 720)]      // zero width
    [InlineData(1280, 0)]     // zero height
    [InlineData(-1280, 720)]  // negative width
    [InlineData(1280, -720)]  // negative height
    [InlineData(1280, null)]  // missing height
    [InlineData(null, 720)]   // missing width
    [InlineData(1922, 1080)]  // exceeds source width
    [InlineData(1920, 1082)]  // exceeds source height
    public void Validate_InvalidTargetResolution_ThrowsInvalidCompressionResolution(int? targetWidth, int? targetHeight)
    {
        CompressionRequest request = CreateRequest(targetWidth: targetWidth, targetHeight: targetHeight);

        ShouldReject(request, CreateVideo(1920, 1080), ApiErrorCodes.InvalidCompressionResolution);
    }

    [Fact]
    public void Validate_UnknownSourceResolution_StillRejectsOddOrNonPositiveDimensions()
    {
        ShouldReject(
            CreateRequest(targetWidth: 1281, targetHeight: 720),
            CreateVideo(width: null, height: null),
            ApiErrorCodes.InvalidCompressionResolution);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-64)]
    [InlineData(31)]
    [InlineData(513)]
    public void Validate_InvalidAudioBitrate_ThrowsInvalidCompressionAudioBitrate(int audioBitrateKbps)
    {
        CompressionRequest request = CreateRequest(audioBitrateKbps: audioBitrateKbps);

        ShouldReject(request, CreateVideo(), ApiErrorCodes.InvalidCompressionAudioBitrate);
    }

    [Fact]
    public void Validate_UndefinedFormat_ThrowsInvalidCompressionFormat()
    {
        ShouldReject(CreateRequest(format: (OutputContainerFormat)99), CreateVideo(), ApiErrorCodes.InvalidCompressionFormat);
    }

    [Fact]
    public void Validate_UndefinedCodec_ThrowsInvalidCompressionCodec()
    {
        ShouldReject(CreateRequest(codec: (VideoCodecOption)99), CreateVideo(), ApiErrorCodes.InvalidCompressionCodec);
    }

    [Fact]
    public void Validate_UndefinedPreset_ThrowsInvalidCompressionPreset()
    {
        ShouldReject(CreateRequest(preset: (EncodingPresetOption)99), CreateVideo(), ApiErrorCodes.InvalidCompressionPreset);
    }

    [Fact]
    public void ServiceProvider_RegisteredWithoutFactory_ResolvesCompressionRequestValidator()
    {
        ServiceCollection services = new();
        services.AddSingleton<ICompressionRequestValidator, CompressionRequestValidator>();
        using ServiceProvider provider = services.BuildServiceProvider();

        ICompressionRequestValidator validator = provider.GetRequiredService<ICompressionRequestValidator>();

        validator.Should().BeOfType<CompressionRequestValidator>();
    }
}
