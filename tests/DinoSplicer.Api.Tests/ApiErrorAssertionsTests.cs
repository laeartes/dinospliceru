using DinoSplicer.Api.Models;

using FluentAssertions;

namespace DinoSplicer.Api.Tests;

public class ApiErrorAssertionsTests
{
    [Fact]
    public void ShouldBeApiErrorJson_ErrorAndCode_ReturnsParsedError()
    {
        ApiError error = ApiErrorAssertions.ShouldBeApiErrorJson("""{ "error": "Video not found", "code": "VIDEO_NOT_FOUND" }""");

        error.Error.Should().Be("Video not found");
        error.Code.Should().Be("VIDEO_NOT_FOUND");
    }

    [Theory]
    [InlineData("""{ "title": "Not Found", "status": 404, "detail": "No video" }""")]
    [InlineData("""{ "error": "Video not found", "code": "VIDEO_NOT_FOUND", "detail": "extra field" }""")]
    [InlineData("""{ "error": "Something failed" }""")]
    [InlineData("""{ "error": "Video not found", "code": "" }""")]
    [InlineData("""{ "error": "", "code": "VIDEO_NOT_FOUND" }""")]
    [InlineData("""{ "error": 42, "code": "VIDEO_NOT_FOUND" }""")]
    [InlineData("""{ "error": "Bad", "code": 1 }""")]
    [InlineData("""[ "error" ]""")]
    public void ShouldBeApiErrorJson_NonConformingShape_Fails(string json)
    {
        Action act = () => ApiErrorAssertions.ShouldBeApiErrorJson(json);

        act.Should().Throw<Exception>();
    }

    [Fact]
    public void ShouldBeApiErrorJson_UnexpectedCode_Fails()
    {
        Action act = () => ApiErrorAssertions.ShouldBeApiErrorJson("""{ "error": "Video not found", "code": "NOT_FOUND" }""", "VIDEO_NOT_FOUND");

        act.Should().Throw<Exception>();
    }
}
