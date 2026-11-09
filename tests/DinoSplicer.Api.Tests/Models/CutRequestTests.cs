using System.ComponentModel.DataAnnotations;
using System.Text.Json;

using DinoSplicer.Api.Models;

using FluentAssertions;

namespace DinoSplicer.Api.Tests.Models;

public class CutRequestTests
{
    private static readonly JsonSerializerOptions CamelCaseOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static List<ValidationResult> Validate(CutRequest request)
    {
        ValidationContext context = new(request);
        List<ValidationResult> results = [];
        Validator.TryValidateObject(request, context, results, validateAllProperties: true);
        return results;
    }

    [Theory]
    [InlineData(0.0, 15.0, CutMode.FastCopy)]
    [InlineData(2.5, 10.0, CutMode.FrameAccurate)]
    public void CutRequest_ValidRangeAndMode_PassesValidation(double startSeconds, double endSeconds, CutMode mode)
    {
        CutRequest request = new()
        {
            StartSeconds = startSeconds,
            EndSeconds = endSeconds,
            Mode = mode
        };

        Validate(request).Should().BeEmpty();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(99)]
    public void CutRequest_UndefinedMode_FailsValidation(int rawMode)
    {
        CutRequest request = new()
        {
            StartSeconds = 0.0,
            EndSeconds = 10.0,
            Mode = (CutMode)rawMode
        };

        Validate(request).Should().Contain(r => r.MemberNames.Contains(nameof(CutRequest.Mode)));
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(-5.0)]
    public void CutRequest_NegativeStartSeconds_FailsValidation(double startSeconds)
    {
        CutRequest request = new()
        {
            StartSeconds = startSeconds,
            EndSeconds = 10.0,
            Mode = CutMode.FastCopy
        };

        Validate(request).Should().Contain(r => r.MemberNames.Contains(nameof(CutRequest.StartSeconds)));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    public void CutRequest_NonPositiveEndSeconds_FailsValidation(double endSeconds)
    {
        CutRequest request = new()
        {
            StartSeconds = 0.0,
            EndSeconds = endSeconds,
            Mode = CutMode.FastCopy
        };

        Validate(request).Should().Contain(r => r.MemberNames.Contains(nameof(CutRequest.EndSeconds)));
    }

    [Theory]
    [InlineData(5.0, 5.0)]
    [InlineData(8.0, 3.0)]
    public void CutRequest_EndSecondsNotGreaterThanStartSeconds_FailsValidation(double startSeconds, double endSeconds)
    {
        CutRequest request = new()
        {
            StartSeconds = startSeconds,
            EndSeconds = endSeconds,
            Mode = CutMode.FrameAccurate
        };

        Validate(request).Should().Contain(r => r.MemberNames.Contains(nameof(CutRequest.EndSeconds)));
    }

    [Theory]
    [InlineData("FastCopy", CutMode.FastCopy)]
    [InlineData("FrameAccurate", CutMode.FrameAccurate)]
    public void CutRequest_JsonDeserialization_ParsesStringMode(string modeJson, CutMode expectedMode)
    {
        string json = $$"""{"startSeconds":1.5,"endSeconds":9.5,"mode":"{{modeJson}}"}""";

        CutRequest? request = JsonSerializer.Deserialize<CutRequest>(json, CamelCaseOptions);

        request.Should().NotBeNull();
        request!.StartSeconds.Should().Be(1.5);
        request.EndSeconds.Should().Be(9.5);
        request.Mode.Should().Be(expectedMode);
    }
}
