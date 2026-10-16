using System.Net;
using System.Text;

using DinoSplicer.Api.Models;

namespace DinoSplicer.Api.Tests.Controllers;

public class ErrorResponseTests(VideoApiFactory factory) : IClassFixture<VideoApiFactory>
{
    [Fact]
    public async Task UnknownRoute_Returns404WithStandardError()
    {
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/api/does-not-exist");

        await response.ShouldBeApiErrorAsync(HttpStatusCode.NotFound, ApiErrorCodes.NotFound);
    }

    [Fact]
    public async Task WrongHttpMethod_Returns405WithStandardError()
    {
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PutAsync("/api/videos", new StringContent(string.Empty));

        await response.ShouldBeApiErrorAsync(HttpStatusCode.MethodNotAllowed, ApiErrorCodes.MethodNotAllowed);
    }

    [Fact]
    public async Task UploadWithJsonBody_Returns415WithStandardError()
    {
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PostAsync("/api/videos", new StringContent("{}", Encoding.UTF8, "application/json"));

        await response.ShouldBeApiErrorAsync(HttpStatusCode.UnsupportedMediaType, ApiErrorCodes.UnsupportedMediaType);
    }
}