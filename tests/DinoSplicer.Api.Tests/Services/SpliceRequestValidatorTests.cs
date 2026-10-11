using DinoSplicer.Api.Models;
using DinoSplicer.Api.Services;

using FluentAssertions;

namespace DinoSplicer.Api.Tests.Services;

public class SpliceRequestValidatorTests
{
    private static Video CreateVideo(double? durationSeconds = 120, long sizeBytes = 10_485_760) => new()
    {
        FileName = "source.mp4",
        OriginalFileName = "source.mp4",
        ContentType = "video/mp4",
        SizeBytes = sizeBytes,
        DurationSeconds = durationSeconds,
        Status = VideoStatus.Ready
    };

    [Theory]
    [InlineData(1.0)]
    [InlineData(30.0)]
    [InlineData(120.0)]
    public void Validate_ByLengthWithinRange_ReturnsNull(double value)
    {
        SpliceRequestValidator.Validate(SpliceMode.ByLength, value, CreateVideo()).Should().BeNull();
    }

    [Theory]
    [InlineData(0.5)]
    [InlineData(120.5)]
    public void Validate_ByLengthOutsideRange_ReturnsError(double value)
    {
        SpliceRequestValidator.Validate(SpliceMode.ByLength, value, CreateVideo()).Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Validate_ByLengthUnknownDuration_SkipsUpperBound()
    {
        SpliceRequestValidator.Validate(SpliceMode.ByLength, 100_000, CreateVideo(durationSeconds: null)).Should().BeNull();
    }

    [Theory]
    [InlineData(1_048_576.0)]
    [InlineData(5_000_000.0)]
    [InlineData(10_485_760.0)]
    public void Validate_BySizeWithinRange_ReturnsNull(double value)
    {
        SpliceRequestValidator.Validate(SpliceMode.BySize, value, CreateVideo()).Should().BeNull();
    }

    [Theory]
    [InlineData(1_000.0)]
    [InlineData(10_485_761.0)]
    public void Validate_BySizeOutsideRange_ReturnsError(double value)
    {
        SpliceRequestValidator.Validate(SpliceMode.BySize, value, CreateVideo()).Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Validate_UndefinedMode_ReturnsError()
    {
        SpliceRequestValidator.Validate((SpliceMode)99, 30, CreateVideo()).Should().NotBeNullOrEmpty();
    }
}
