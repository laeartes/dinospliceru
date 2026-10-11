using System.ComponentModel.DataAnnotations;

using DinoSplicer.Api.Models;

using FluentAssertions;

namespace DinoSplicer.Api.Tests.Models;

public class SpliceRequestTests
{
    private static List<ValidationResult> Validate(SpliceRequest request)
    {
        ValidationContext context = new(request);
        List<ValidationResult> results = [];
        Validator.TryValidateObject(request, context, results, validateAllProperties: true);
        return results;
    }

    [Theory]
    [InlineData(SpliceMode.ByLength, 30.0)]
    [InlineData(SpliceMode.BySize, 5_000_000.0)]
    public void SpliceRequest_ValidModeAndValue_PassesValidation(SpliceMode mode, double value)
    {
        SpliceRequest request = new() { Mode = mode, Value = value };

        Validate(request).Should().BeEmpty();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(99)]
    public void SpliceRequest_UndefinedMode_FailsValidation(int rawMode)
    {
        SpliceRequest request = new() { Mode = (SpliceMode)rawMode, Value = 30 };

        Validate(request).Should().Contain(r => r.MemberNames.Contains(nameof(SpliceRequest.Mode)));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    public void SpliceRequest_NonPositiveValue_FailsValidation(double value)
    {
        SpliceRequest request = new() { Mode = SpliceMode.ByLength, Value = value };

        Validate(request).Should().Contain(r => r.MemberNames.Contains(nameof(SpliceRequest.Value)));
    }
}
