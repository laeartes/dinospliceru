using DinoSplicer.Api.Models;

using FluentAssertions;

namespace DinoSplicer.Api.Tests.Models;

public class ApiErrorCodesTests
{
    [Theory]
    [InlineData(VideoFileError.Empty, ApiErrorCodes.FileEmpty)]
    [InlineData(VideoFileError.TooLarge, ApiErrorCodes.FileTooLarge)]
    [InlineData(VideoFileError.FileNameTooLong, ApiErrorCodes.FileNameTooLong)]
    [InlineData(VideoFileError.ExtensionNotAllowed, ApiErrorCodes.UnsupportedFileExtension)]
    [InlineData(VideoFileError.ContentTypeNotAllowed, ApiErrorCodes.UnsupportedContentType)]
    [InlineData(VideoFileError.None, ApiErrorCodes.InvalidUpload)]
    public void ForVideoFileError_EachReason_ReturnsMatchingCode(VideoFileError reason, string expectedCode)
    {
        ApiErrorCodes.ForVideoFileError(reason).Should().Be(expectedCode);
    }
}
