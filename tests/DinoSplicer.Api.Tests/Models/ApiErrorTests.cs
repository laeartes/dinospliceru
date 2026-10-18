using System.Text.Json;

using DinoSplicer.Api.Models;

using FluentAssertions;

using Microsoft.AspNetCore.Http;

namespace DinoSplicer.Api.Tests.Models;

public class ApiErrorTests
{
    [Fact]
    public void Serialize_ApiError_WritesErrorAndCode()
    {
        string json = JsonSerializer.Serialize(new ApiError("Video not found", "VIDEO_NOT_FOUND"), JsonSerializerOptions.Web);

        json.Should().Be("""{"error":"Video not found","code":"VIDEO_NOT_FOUND"}""");
    }

    [Theory]
    [InlineData(StatusCodes.Status404NotFound, ApiErrorCodes.NotFound)]
    [InlineData(StatusCodes.Status405MethodNotAllowed, ApiErrorCodes.MethodNotAllowed)]
    [InlineData(StatusCodes.Status415UnsupportedMediaType, ApiErrorCodes.UnsupportedMediaType)]
    [InlineData(StatusCodes.Status500InternalServerError, ApiErrorCodes.InternalError)]
    [InlineData(StatusCodes.Status503ServiceUnavailable, ApiErrorCodes.InternalError)]
    [InlineData(StatusCodes.Status418ImATeapot, ApiErrorCodes.RequestFailed)]
    public void ForStatusCode_AnyStatus_ConformsToSharedShape(int statusCode, string expectedCode)
    {
        string json = JsonSerializer.Serialize(ApiError.ForStatusCode(statusCode), JsonSerializerOptions.Web);

        ApiErrorAssertions.ShouldBeApiErrorJson(json, expectedCode);
    }
}
