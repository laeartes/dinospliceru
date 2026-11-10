using System.ComponentModel.DataAnnotations;

using DinoSplicer.Api.Models;

using FluentAssertions;

namespace DinoSplicer.Api.Tests.Models;

public class CompressionJobTests
{
    private static List<ValidationResult> Validate(CompressionJob job)
    {
        ValidationContext context = new(job);
        List<ValidationResult> results = [];
        Validator.TryValidateObject(job, context, results, validateAllProperties: true);
        return results;
    }

    private static CompressionJob CreateValidJob()
    {
        return new CompressionJob
        {
            SourceVideoId = Guid.NewGuid(),
            Format = OutputContainerFormat.Mp4,
            VideoCodec = VideoCodecOption.H264,
            Crf = 23,
            Preset = EncodingPresetOption.Medium
        };
    }

    [Fact]
    public void CompressionJob_OriginalSizeAndAudio_PassesValidation()
    {
        Validate(CreateValidJob()).Should().BeEmpty();
    }

    [Fact]
    public void CompressionJob_AllCustomParametersSet_PassesValidation()
    {
        CompressionJob job = CreateValidJob();
        job.Format = OutputContainerFormat.Webm;
        job.VideoCodec = VideoCodecOption.Vp9;
        job.Crf = 31;
        job.TargetWidth = 1280;
        job.TargetHeight = 720;
        job.AudioBitrateKbps = 128;
        job.Preset = EncodingPresetOption.Slow;

        Validate(job).Should().BeEmpty();
    }

    [Theory]
    [InlineData(CompressionJob.MinCrf)]
    [InlineData(CompressionJob.MaxCrf)]
    public void CompressionJob_CrfAtBounds_PassesValidation(int crf)
    {
        CompressionJob job = CreateValidJob();
        job.Crf = crf;

        Validate(job).Should().BeEmpty();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(64)]
    public void CompressionJob_CrfOutOfRange_FailsValidation(int crf)
    {
        CompressionJob job = CreateValidJob();
        job.Crf = crf;

        Validate(job).Should().Contain(r => r.MemberNames.Contains(nameof(CompressionJob.Crf)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-720)]
    public void CompressionJob_NonPositiveTargetSize_FailsValidation(int size)
    {
        CompressionJob job = CreateValidJob();
        job.TargetWidth = size;
        job.TargetHeight = size;

        List<ValidationResult> results = Validate(job);

        results.Should().Contain(r => r.MemberNames.Contains(nameof(CompressionJob.TargetWidth)));
        results.Should().Contain(r => r.MemberNames.Contains(nameof(CompressionJob.TargetHeight)));
    }

    [Theory]
    [InlineData(31)]
    [InlineData(513)]
    public void CompressionJob_AudioBitrateOutOfRange_FailsValidation(int audioBitrateKbps)
    {
        CompressionJob job = CreateValidJob();
        job.AudioBitrateKbps = audioBitrateKbps;

        Validate(job).Should().Contain(r => r.MemberNames.Contains(nameof(CompressionJob.AudioBitrateKbps)));
    }

    [Fact]
    public void CompressionJob_UndefinedEnumValues_FailValidation()
    {
        CompressionJob job = CreateValidJob();
        job.Format = (OutputContainerFormat)99;
        job.VideoCodec = (VideoCodecOption)99;
        job.Preset = (EncodingPresetOption)99;
        job.Status = (CompressionJobStatus)99;

        List<ValidationResult> results = Validate(job);

        results.Should().Contain(r => r.MemberNames.Contains(nameof(CompressionJob.Format)));
        results.Should().Contain(r => r.MemberNames.Contains(nameof(CompressionJob.VideoCodec)));
        results.Should().Contain(r => r.MemberNames.Contains(nameof(CompressionJob.Preset)));
        results.Should().Contain(r => r.MemberNames.Contains(nameof(CompressionJob.Status)));
    }

    [Fact]
    public void CompressionJob_NoStatusSpecified_DefaultsToPending()
    {
        CreateValidJob().Status.Should().Be(CompressionJobStatus.Pending);
    }

    [Fact]
    public void CompressionJob_StringsExceedMaxLength_FailsValidation()
    {
        CompressionJob job = CreateValidJob();
        job.OutputFileName = new string('a', 256);
        job.OutputStoragePath = new string('a', 1025);
        job.ErrorMessage = new string('a', 501);

        List<ValidationResult> results = Validate(job);

        results.Should().Contain(r => r.MemberNames.Contains(nameof(CompressionJob.OutputFileName)));
        results.Should().Contain(r => r.MemberNames.Contains(nameof(CompressionJob.OutputStoragePath)));
        results.Should().Contain(r => r.MemberNames.Contains(nameof(CompressionJob.ErrorMessage)));
    }

    [Fact]
    public void GetOutputDirectory_ValidStorageAndJobId_ReturnsCompressionsSubdirectory()
    {
        Guid jobId = Guid.NewGuid();

        string directory = CompressionJob.GetOutputDirectory("/storage", jobId);

        directory.Should().Be(Path.Combine("/storage", "compressions", jobId.ToString()));
    }
}