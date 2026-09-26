using System.ComponentModel.DataAnnotations;

using DinoSplicer.Api.Models;

using FluentAssertions;

namespace DinoSplicer.Api.Tests.Models;

public class VideoTests
{
    private static IList<ValidationResult> Validate(Video video)
    {
        var context = new ValidationContext(video);
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(video, context, results, validateAllProperties: true);
        return results;
    }

    private static Video CreateValidVideo() => new()
    {
        FileName = "abc123.mp4",
        OriginalFileName = "my vacation video.mp4",
        ContentType = "video/mp4",
        SizeBytes = 10_485_760,
        StoragePath = "/storage/videos/abc123.mp4",
        Status = VideoStatus.Uploaded
    };

    [Fact]
    public void Video_AllFieldsValid_PassesValidation()
    {
        var results = Validate(CreateValidVideo());
        results.Should().BeEmpty();
    }

    [Fact]
    public void Video_MissingFileName_FailsValidation()
    {
        var video = CreateValidVideo();
        video.FileName = "";

        var results = Validate(video);

        results.Should().Contain(r => r.MemberNames.Contains(nameof(Video.FileName)));
    }

    [Fact]
    public void Video_NegativeSizeBytes_FailsValidation()
    {
        var video = CreateValidVideo();
        video.SizeBytes = -1;

        var results = Validate(video);

        results.Should().Contain(r => r.MemberNames.Contains(nameof(Video.SizeBytes)));
    }

    [Theory]
    [InlineData(VideoStatus.Uploaded)]
    [InlineData(VideoStatus.Processing)]
    [InlineData(VideoStatus.Ready)]
    [InlineData(VideoStatus.Failed)]
    public void VideoStatus_AnyDefinedValue_IsValidEnumMember(VideoStatus status)
    {
        Enum.IsDefined(typeof(VideoStatus), status).Should().BeTrue();
    }

    [Fact]
    public void Video_NoStatusSpecified_DefaultsToUploaded()
    {
        var video = new Video
        {
            FileName = "a.mp4",
            OriginalFileName = "a.mp4",
            ContentType = "video/mp4",
            StoragePath = "/x"
        };

        video.Status.Should().Be(VideoStatus.Uploaded);
    }

}
