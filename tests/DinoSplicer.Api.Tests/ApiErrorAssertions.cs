using System.Net;
using System.Text.Json;

using DinoSplicer.Api.Models;

using FluentAssertions;

namespace DinoSplicer.Api.Tests;

public static class ApiErrorAssertions
{
    public static async Task<ApiError> ShouldBeApiErrorAsync(
        this HttpResponseMessage response,
        HttpStatusCode expectedStatus,
        string? expectedCode = null
    )
    {
        response.StatusCode.Should().Be(expectedStatus);
        response.Content.Headers.ContentType.Should().NotBeNull();
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");

        string json = await response.Content.ReadAsStringAsync();
        return ShouldBeApiErrorJson(json, expectedCode);
    }

    public static ApiError ShouldBeApiErrorJson(string json, string? expectedCode = null)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;

        root.ValueKind.Should().Be(JsonValueKind.Object, "an error response must be a JSON object");

        List<string> propertyNames = root.EnumerateObject().Select(property => property.Name).ToList();
        propertyNames.Should().Contain("error");
        propertyNames.Should().Contain("code");
        propertyNames.Should().BeSubsetOf(["error", "code"], "an error response may only contain 'error' and 'code'");

        JsonElement errorElement = root.GetProperty("error");
        errorElement.ValueKind.Should().Be(JsonValueKind.String);
        string message = errorElement.GetString()!;
        message.Should().NotBeNullOrWhiteSpace();

        JsonElement codeElement = root.GetProperty("code");
        codeElement.ValueKind.Should().Be(JsonValueKind.String);
        string code = codeElement.GetString()!;
        code.Should().NotBeNullOrWhiteSpace();

        if (expectedCode is not null)
        {
            code.Should().Be(expectedCode);
        }

        return new ApiError(message, code);
    }
}