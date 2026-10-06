using System.ComponentModel.DataAnnotations;

using DinoSplicer.Api.Models;

using FluentAssertions;

namespace DinoSplicer.Api.Tests.Models;

public class SpliceJobTests
{
    private static List<ValidationResult> Validate(SpliceJob job)
    {
        ValidationContext context = new(job);
        List<ValidationResult> results = [];
        Validator.TryValidateObject(job, context, results, validateAllProperties: true);
        return results;
    }

    private static SpliceJob CreateValidJob() => new()
    {
        SourceVideoId = Guid.NewGuid(),
        Mode = SpliceMode.ByLength,
        SegmentValue = 60
    };

    [Fact]
    public void SpliceJob_AllFieldsValid_PassesValidation()
    {
        Validate(CreateValidJob()).Should().BeEmpty();
    }

    [Theory]
    [InlineData(SpliceMode.ByLength)]
    [InlineData(SpliceMode.BySize)]
    public void SpliceJob_DefinedMode_PassesValidation(SpliceMode mode)
    {
        SpliceJob job = CreateValidJob();
        job.Mode = mode;

        Validate(job).Should().BeEmpty();
    }

    [Theory]
    [InlineData(0)]  // default value of an unset Mode
    [InlineData(99)]
    public void SpliceJob_UndefinedMode_FailsValidation(int rawMode)
    {
        SpliceJob job = CreateValidJob();
        job.Mode = (SpliceMode)rawMode;

        List<ValidationResult> results = Validate(job);

        results.Should().Contain(r => r.MemberNames.Contains(nameof(SpliceJob.Mode)));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    [InlineData(-0.5)]
    public void SpliceJob_NonPositiveSegmentValue_FailsValidation(double segmentValue)
    {
        SpliceJob job = CreateValidJob();
        job.SegmentValue = segmentValue;

        List<ValidationResult> results = Validate(job);

        results.Should().Contain(r => r.MemberNames.Contains(nameof(SpliceJob.SegmentValue)));
    }

    [Theory]
    [InlineData(SpliceJobStatus.Pending)]
    [InlineData(SpliceJobStatus.Processing)]
    [InlineData(SpliceJobStatus.Ready)]
    [InlineData(SpliceJobStatus.Failed)]
    public void SpliceJob_DefinedStatus_PassesValidation(SpliceJobStatus status)
    {
        SpliceJob job = CreateValidJob();
        job.Status = status;

        Validate(job).Should().BeEmpty();
    }

    [Fact]
    public void SpliceJob_UndefinedStatus_FailsValidation()
    {
        SpliceJob job = CreateValidJob();
        job.Status = (SpliceJobStatus)99;

        List<ValidationResult> results = Validate(job);

        results.Should().Contain(r => r.MemberNames.Contains(nameof(SpliceJob.Status)));
    }

    [Fact]
    public void SpliceJob_NoStatusSpecified_DefaultsToPending()
    {
        SpliceJob job = new();

        job.Status.Should().Be(SpliceJobStatus.Pending);
    }

    [Fact]
    public void SpliceJob_NewInstance_HasNoOutputs()
    {
        SpliceJob job = new();

        job.Outputs.Should().BeEmpty();
    }
}
