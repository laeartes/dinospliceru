using System.Net;

using FluentAssertions;

namespace DinoSplicer.Api.Tests.Controllers;

public class CorsTests(VideoApiFactory factory) : IClassFixture<VideoApiFactory>
{
    private const string ApiEndpoint = "/api/config";
    private const string ViteDevOrigin = "http://localhost:5173";
    private const string UntrustedOrigin = "http://untrusted-site.com";

    [Fact]
    public async Task PreflightOptions_ViteDevOrigin_ReturnsNoContentWithCorsHeaders()
    {
        HttpClient client = factory.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Options, ApiEndpoint);
        request.Headers.Add("Origin", ViteDevOrigin);
        request.Headers.Add("Access-Control-Request-Method", "GET");
        request.Headers.Add("Access-Control-Request-Headers", "content-type");

        HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        response.Headers.GetValues("Access-Control-Allow-Origin").Should().ContainSingle().Which.Should().Be(ViteDevOrigin);
        response.Headers.GetValues("Access-Control-Allow-Credentials").Should().ContainSingle().Which.Should().Be("true");
        response.Headers.Contains("Access-Control-Allow-Methods").Should().BeTrue();
    }

    [Fact]
    public async Task GetRequest_ViteDevOrigin_ReturnsOkWithCorsHeaders()
    {
        HttpClient client = factory.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Get, ApiEndpoint);
        request.Headers.Add("Origin", ViteDevOrigin);

        HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.GetValues("Access-Control-Allow-Origin").Should().ContainSingle().Which.Should().Be(ViteDevOrigin);
        response.Headers.GetValues("Access-Control-Allow-Credentials").Should().ContainSingle().Which.Should().Be("true");
    }

    [Fact]
    public async Task PreflightOptions_UntrustedOrigin_DoesNotReturnCorsHeaders()
    {
        HttpClient client = factory.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Options, ApiEndpoint);
        request.Headers.Add("Origin", UntrustedOrigin);
        request.Headers.Add("Access-Control-Request-Method", "GET");

        HttpResponseMessage response = await client.SendAsync(request);

        response.Headers.Contains("Access-Control-Allow-Origin").Should().BeFalse();
    }
}
