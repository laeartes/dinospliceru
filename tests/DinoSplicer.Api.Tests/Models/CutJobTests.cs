using System.ComponentModel.DataAnnotations;

using DinoSplicer.Api.Models;

using FluentAssertions;

namespace DinoSplicer.Api.Tests.Models;

public class CutJobTests
{
    private static List<ValidationResult> Validate(CutJob job)
    {
        ValidationContext context = new(job);
        List<ValidationResult> results = [];
        Validator.TryValidateObject(job, context, results, validateAllProperties: true);
        return results;
    }

    private static CutJob CreateValidJob()
    {
        return new CutJob
        {
            SourceVideoId = Guid.NewGuid(),
            StartSeconds = 0.0,
            EndSeconds = 30.0,
            Mode = CutMode.FastCopy
        };
    }

    [Fact]
    public void CutJob_AllFieldsValid_PassesValidation()
    {
        Validate(CreateValidJob()).Should().BeEmpty();
    }

    [Theory]
    [InlineData(CutMode.FastCopy)]
    [InlineData(CutMode.FrameAccurate)]
    public void CutJob_DefinedMode_PassesValidation(CutMode mode)
    {
        CutJob job = CreateValidJob();
        job.Mode = mode;

        Validate(job).Should().BeEmpty();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(99)]
    public void CutJob_UndefinedMode_FailsValidation(int rawMode)
    {
        CutJob job = CreateValidJob();
        job.Mode = (CutMode)rawMode;

        List<ValidationResult> results = Validate(job);

        results.Should().Contain(r => r.MemberNames.Contains(nameof(CutJob.Mode)));
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(-10.0)]
    public void CutJob_NegativeStartSeconds_FailsValidation(double startSeconds)
    {
        CutJob job = CreateValidJob();
        job.StartSeconds = startSeconds;

        List<ValidationResult> results = Validate(job);

        results.Should().Contain(r => r.MemberNames.Contains(nameof(CutJob.StartSeconds)));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    public void CutJob_NonPositiveEndSeconds_FailsValidation(double endSeconds)
    {
        CutJob job = CreateValidJob();
        job.EndSeconds = endSeconds;

        List<ValidationResult> results = Validate(job);

        results.Should().Contain(r => r.MemberNames.Contains(nameof(CutJob.EndSeconds)));
    }

    [Theory]
    [InlineData(10.0, 10.0)]
    [InlineData(10.0, 5.0)]
    public void CutJob_EndSecondsNotGreaterThanStartSeconds_FailsValidation(double startSeconds, double endSeconds)
    {
        CutJob job = CreateValidJob();
        job.StartSeconds = startSeconds;
        job.EndSeconds = endSeconds;

        List<ValidationResult> results = Validate(job);

        results.Should().Contain(r => r.MemberNames.Contains(nameof(CutJob.EndSeconds)));
    }

    [Theory]
    [InlineData(CutJobStatus.Pending)]
    [InlineData(CutJobStatus.Processing)]
    [InlineData(CutJobStatus.Ready)]
    [InlineData(CutJobStatus.Failed)]
    public void CutJob_DefinedStatus_PassesValidation(CutJobStatus status)
    {
        CutJob job = CreateValidJob();
        job.Status = status;

        Validate(job).Should().BeEmpty();
    }

    [Fact]
    public void CutJob_UndefinedStatus_FailsValidation()
    {
        CutJob job = CreateValidJob();
        job.Status = (CutJobStatus)99;

        List<ValidationResult> results = Validate(job);

        results.Should().Contain(r => r.MemberNames.Contains(nameof(CutJob.Status)));
    }

    [Fact]
    public void CutJob_NoStatusSpecified_DefaultsToPending()
    {
        CutJob job = new();

        job.Status.Should().Be(CutJobStatus.Pending);
    }

    [Fact]
    public void CutJob_NegativeOutputDurationSeconds_FailsValidation()
    {
        CutJob job = CreateValidJob();
        job.OutputDurationSeconds = -1.0;

        List<ValidationResult> results = Validate(job);

        results.Should().Contain(r => r.MemberNames.Contains(nameof(CutJob.OutputDurationSeconds)));
    }

    [Fact]
    public void CutJob_NegativeOutputSizeBytes_FailsValidation()
    {
        CutJob job = CreateValidJob();
        job.OutputSizeBytes = -1;

        List<ValidationResult> results = Validate(job);

        results.Should().Contain(r => r.MemberNames.Contains(nameof(CutJob.OutputSizeBytes)));
    }

    [Fact]
    public void CutJob_StringsExceedMaxLength_FailsValidation()
    {
        CutJob job = CreateValidJob();
        job.OutputFileName = new string('a', 256);
        job.OutputStoragePath = new string('b', 1025);
        job.ErrorMessage = new string('c', 501);

        List<ValidationResult> results = Validate(job);

        results.Should().Contain(r => r.MemberNames.Contains(nameof(CutJob.OutputFileName)));
        results.Should().Contain(r => r.MemberNames.Contains(nameof(CutJob.OutputStoragePath)));
        results.Should().Contain(r => r.MemberNames.Contains(nameof(CutJob.ErrorMessage)));
    }

    [Fact]
    public void GetOutputDirectory_ValidStorageAndJobId_ReturnsCutsSubdirectory()
    {
        Guid jobId = Guid.NewGuid();

        string path = CutJob.GetOutputDirectory("/tmp/storage", jobId);

        path.Should().Be(Path.Combine("/tmp/storage", "cuts", jobId.ToString()));
    }
}
