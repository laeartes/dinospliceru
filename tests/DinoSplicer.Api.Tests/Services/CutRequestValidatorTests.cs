using System.Net;

using DinoSplicer.Api.Exceptions;
using DinoSplicer.Api.Models;
using DinoSplicer.Api.Services;

using FluentAssertions;

using Microsoft.Extensions.DependencyInjection;

namespace DinoSplicer.Api.Tests.Services;

public class CutRequestValidatorTests
{
    private readonly CutRequestValidator _validator = new();

    private static Video CreateVideo(double? durationSeconds = 120) => new()
    {
        FileName = "source.mp4",
        OriginalFileName = "source.mp4",
        ContentType = "video/mp4",
        SizeBytes = 10_485_760,
        DurationSeconds = durationSeconds,
        Status = VideoStatus.Ready
    };

    private static CutRequest CreateRequest(double startSeconds, double endSeconds, CutMode mode = CutMode.FastCopy) => new()
    {
        StartSeconds = startSeconds,
        EndSeconds = endSeconds,
        Mode = mode
    };

    private void ShouldReject(CutRequest request, Video video, string expectedCode)
    {
        Action act = () => _validator.Validate(request, video);

        DomainValidationException exception = act.Should().Throw<DomainValidationException>().Which;
        exception.ErrorCode.Should().Be(expectedCode);
        exception.StatusCode.Should().Be((int)HttpStatusCode.BadRequest);
        exception.Message.Should().NotBeNullOrWhiteSpace();
    }

    [Theory]
    [InlineData(0.0, 15.0, CutMode.FastCopy)]
    [InlineData(2.5, 10.0, CutMode.FrameAccurate)]
    [InlineData(0.0, 120.0, CutMode.FastCopy)]       // the whole video
    [InlineData(10.0, 10.1, CutMode.FrameAccurate)]  // the minimum length; 10.1 - 10.0 is slightly below 0.1 in floating point
    [InlineData(119.9, 120.0, CutMode.FastCopy)]     // ends exactly at the duration
    [InlineData(100.0, 120.0005, CutMode.FastCopy)]  // past the duration, but within the tolerance
    public void Validate_ValidRange_DoesNotThrow(double startSeconds, double endSeconds, CutMode mode)
    {
        Action act = () => _validator.Validate(CreateRequest(startSeconds, endSeconds, mode), CreateVideo());

        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(-0.1, 10.0)]   // start < 0
    [InlineData(-5.0, 10.0)]
    [InlineData(5.0, 5.0)]     // start == end
    [InlineData(0.0, 0.0)]
    [InlineData(8.0, 3.0)]     // start > end
    [InlineData(0.0, 0.05)]    // shorter than the minimum clip length
    [InlineData(10.0, 10.05)]
    [InlineData(0.0, 120.5)]   // end > duration
    [InlineData(100.0, 200.0)]
    [InlineData(130.0, 140.0)] // entirely past the end
    public void Validate_InvalidRange_ThrowsInvalidCutRange(double startSeconds, double endSeconds)
    {
        ShouldReject(CreateRequest(startSeconds, endSeconds), CreateVideo(), ApiErrorCodes.InvalidCutRange);
    }

    [Theory]
    [InlineData(double.NaN, 10.0)]
    [InlineData(0.0, double.NaN)]
    [InlineData(double.NaN, double.NaN)]
    [InlineData(0.0, double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity, 10.0)]
    [InlineData(double.PositiveInfinity, double.PositiveInfinity)]
    public void Validate_NonFiniteValue_ThrowsInvalidCutRange(double startSeconds, double endSeconds)
    {
        ShouldReject(CreateRequest(startSeconds, endSeconds), CreateVideo(), ApiErrorCodes.InvalidCutRange);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(99)]
    public void Validate_UndefinedMode_ThrowsInvalidCutMode(int rawMode)
    {
        ShouldReject(CreateRequest(0.0, 10.0, (CutMode)rawMode), CreateVideo(), ApiErrorCodes.InvalidCutMode);
    }

    [Fact]
    public void Validate_UnknownDuration_SkipsUpperBound()
    {
        Action act = () => _validator.Validate(CreateRequest(0.0, 100_000.0), CreateVideo(durationSeconds: null));

        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_UnknownDuration_StillRejectsInvalidRange()
    {
        ShouldReject(CreateRequest(5.0, 5.0), CreateVideo(durationSeconds: null), ApiErrorCodes.InvalidCutRange);
    }

    [Fact]
    public void Validate_EndPastDuration_MessageMentionsDuration()
    {
        Action act = () => _validator.Validate(CreateRequest(0.0, 120.5), CreateVideo());

        act.Should().Throw<DomainValidationException>().WithMessage("*duration*120*");
    }

    [Fact]
    public void ServiceProvider_RegisteredWithoutFactory_ResolvesCutRequestValidator()
    {
        ServiceCollection services = new();
        services.AddSingleton<ICutRequestValidator, CutRequestValidator>();
        using ServiceProvider provider = services.BuildServiceProvider();

        ICutRequestValidator validator = provider.GetRequiredService<ICutRequestValidator>();

        validator.Should().BeOfType<CutRequestValidator>();
    }
}
