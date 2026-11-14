using System.Net;
using System.Text;

using DinoSplicer.Api.Data;
using DinoSplicer.Api.Models;

using FluentAssertions;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

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

    [Fact]
    public async Task UnhandledException_Returns500WithSafeApiErrorWithoutStackTrace()
    {
        await using WebApplicationFactory<Program> failingFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
                services.AddScoped<AppDbContext>(_ => throw new InvalidOperationException("secret db failure"))));
        HttpClient client = failingFactory.CreateClient();

        HttpResponseMessage response = await client.GetAsync($"/api/splice-jobs/{Guid.NewGuid()}");

        ApiError error = await response.ShouldBeApiErrorAsync(HttpStatusCode.InternalServerError, ApiErrorCodes.InternalError);
        error.Error.Should().Be("An unexpected error occurred.");
        error.Error.Should().NotContain("secret");
    }
}
