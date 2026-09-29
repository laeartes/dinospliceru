using DinoSplicer.Api.Extensions;

using FluentAssertions;

using Microsoft.AspNetCore.Http;

namespace DinoSplicer.Api.Tests.Extensions;

public class FormFileExtensionsTests
{
    private const long MaxSizeBytes = 1000;
    private static readonly string[] AllowedExtensions = [".mp4", ".mov", ".mkv", ".webm"];
    private static readonly string[] AllowedContentTypes = ["video/mp4", "video/quicktime", "video/x-matroska", "video/webm"];

    [Theory]
    [InlineData("clip.mp4", "video/mp4")]
    [InlineData("clip.mov", "video/quicktime")]
    [InlineData("clip.mkv", "video/x-matroska")]
    [InlineData("clip.webm", "video/webm")]
    [InlineData("CLIP.mp4", "video/mp4")]
    public void IsValidVideoFile_AllowedFile_ReturnsTrue(string fileName, string contentType)
    {
        IFormFile file = CreateFile(fileName, contentType, 500);

        bool isValid = file.IsValidVideoFile(AllowedExtensions, AllowedContentTypes, MaxSizeBytes, out string? error);

        isValid.Should().BeTrue();
        error.Should().BeNull();
    }

    [Fact]
    public void IsValidVideoFile_ExactlyMaxSize_ReturnsTrue()
    {
        IFormFile file = CreateFile("clip.mp4", "video/mp4", MaxSizeBytes);

        file.IsValidVideoFile(AllowedExtensions, AllowedContentTypes, MaxSizeBytes, out _).Should().BeTrue();
    }

    [Fact]
    public void IsValidVideoFile_OverMaxSize_ReturnsFalseWithSizeError()
    {
        IFormFile file = CreateFile("clip.mp4", "video/mp4", MaxSizeBytes + 1);

        bool isValid = file.IsValidVideoFile(AllowedExtensions, AllowedContentTypes, MaxSizeBytes, out string? error);

        isValid.Should().BeFalse();
        error.Should().Contain("maximum allowed size");
    }

    [Fact]
    public void IsValidVideoFile_EmptyFile_ReturnsFalse()
    {
        IFormFile file = CreateFile("clip.mp4", "video/mp4", 0);

        file.IsValidVideoFile(AllowedExtensions, AllowedContentTypes, MaxSizeBytes, out string? error).Should().BeFalse();
        error.Should().Contain("empty");
    }

    [Theory]
    [InlineData("clip.avi")]
    [InlineData("notes.txt")]
    [InlineData("noextension")]
    public void IsValidVideoFile_DisallowedExtension_ReturnsFalse(string fileName)
    {
        IFormFile file = CreateFile(fileName, "video/mp4", 500);

        file.IsValidVideoFile(AllowedExtensions, AllowedContentTypes, MaxSizeBytes, out string? error).Should().BeFalse();
        error.Should().Contain("extension");
    }

    [Fact]
    public void IsValidVideoFile_DisallowedContentType_ReturnsFalse()
    {
        IFormFile file = CreateFile("clip.mp4", "text/plain", 500);

        file.IsValidVideoFile(AllowedExtensions, AllowedContentTypes, MaxSizeBytes, out string? error).Should().BeFalse();
        error.Should().Contain("content type");
    }

    private static FormFile CreateFile(string fileName, string contentType, long length)
    {
        return new FormFile(Stream.Null, 0, length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType,
        };
    }
}
