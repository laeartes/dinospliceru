using System.ComponentModel.DataAnnotations;
using System.Text.Json;

using DinoSplicer.Api.Models;

using FluentAssertions;

namespace DinoSplicer.Api.Tests.Models;

public class CompressionRequestTests
{
    private static readonly JsonSerializerOptions CamelCaseOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static List<ValidationResult> Validate(CompressionRequest request)
    {
        ValidationContext context = new(request);
        List<ValidationResult> results = [];
        Validator.TryValidateObject(request, context, results, validateAllProperties: true);
        return results;
    }

    private static CompressionRequest Deserialize(string json)
    {
        CompressionRequest? request = JsonSerializer.Deserialize<CompressionRequest>(json, CamelCaseOptions);
        request.Should().NotBeNull();
        return request!;
    }

    [Fact]
    public void CompressionRequest_EmptyBody_UsesDefaultsAndPassesValidation()
    {
        CompressionRequest request = Deserialize("{}");

        request.Format.Should().Be(OutputContainerFormat.Mp4);
        request.VideoCodec.Should().Be(VideoCodecOption.H264);
        request.Crf.Should().Be(CompressionRequest.DefaultCrf);
        request.Preset.Should().Be(EncodingPresetOption.Medium);
        request.TargetWidth.Should().BeNull();
        request.TargetHeight.Should().BeNull();
        request.AudioBitrateKbps.Should().BeNull();
        Validate(request).Should().BeEmpty();
    }

    [Fact]
    public void CompressionRequest_CrfOnly_KeepsOtherDefaults()
    {
        CompressionRequest request = Deserialize("""{"crf":28}""");

        request.Crf.Should().Be(28);
        request.Format.Should().Be(OutputContainerFormat.Mp4);
        request.VideoCodec.Should().Be(VideoCodecOption.H264);
        request.Preset.Should().Be(EncodingPresetOption.Medium);
        request.TargetWidth.Should().BeNull();
        Validate(request).Should().BeEmpty();
    }

    [Fact]
    public void CompressionRequest_AllParameters_ParsesStringEnumsAndValues()
    {
        CompressionRequest request = Deserialize(
            """{"format":"Webm","videoCodec":"Vp9","crf":31,"targetWidth":1280,"targetHeight":720,"audioBitrateKbps":96,"preset":"Slow"}""");

        request.Format.Should().Be(OutputContainerFormat.Webm);
        request.VideoCodec.Should().Be(VideoCodecOption.Vp9);
        request.Crf.Should().Be(31);
        request.TargetWidth.Should().Be(1280);
        request.TargetHeight.Should().Be(720);
        request.AudioBitrateKbps.Should().Be(96);
        request.Preset.Should().Be(EncodingPresetOption.Slow);
        Validate(request).Should().BeEmpty();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(64)]
    public void CompressionRequest_CrfOutOfRange_FailsValidation(int crf)
    {
        CompressionRequest request = new() { Crf = crf };

        Validate(request).Should().Contain(r => r.MemberNames.Contains(nameof(CompressionRequest.Crf)));
    }

    [Fact]
    public void CompressionRequest_NonPositiveTargetSize_FailsValidation()
    {
        CompressionRequest request = new() { TargetWidth = 0, TargetHeight = -720 };

        List<ValidationResult> results = Validate(request);

        results.Should().Contain(r => r.MemberNames.Contains(nameof(CompressionRequest.TargetWidth)));
        results.Should().Contain(r => r.MemberNames.Contains(nameof(CompressionRequest.TargetHeight)));
    }

    [Theory]
    [InlineData(31)]
    [InlineData(513)]
    public void CompressionRequest_AudioBitrateOutOfRange_FailsValidation(int audioBitrateKbps)
    {
        CompressionRequest request = new() { AudioBitrateKbps = audioBitrateKbps };

        Validate(request).Should().Contain(r => r.MemberNames.Contains(nameof(CompressionRequest.AudioBitrateKbps)));
    }

    [Fact]
    public void CompressionRequest_UndefinedEnumValues_FailValidation()
    {
        CompressionRequest request = new()
        {
            Format = (OutputContainerFormat)99,
            VideoCodec = (VideoCodecOption)99,
            Preset = (EncodingPresetOption)99
        };

        List<ValidationResult> results = Validate(request);

        results.Should().Contain(r => r.MemberNames.Contains(nameof(CompressionRequest.Format)));
        results.Should().Contain(r => r.MemberNames.Contains(nameof(CompressionRequest.VideoCodec)));
        results.Should().Contain(r => r.MemberNames.Contains(nameof(CompressionRequest.Preset)));
    }
}
