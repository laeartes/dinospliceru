using DinoSplicer.Api.Exceptions;
using DinoSplicer.Api.Models;

using FluentAssertions;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DinoSplicer.Api.Tests.Exceptions;

public class DomainExceptionHandlerTests
{
    public static TheoryData<DinoSplicerException, int, string> DomainExceptions => new()
    {
        {
            new ResourceNotFoundException("No video with id '123' exists.", ApiErrorCodes.VideoNotFound),
            StatusCodes.Status404NotFound,
            ApiErrorCodes.VideoNotFound
        },
        {
            new ResourceConflictException("The video must be Ready.", ApiErrorCodes.VideoNotReady),
            StatusCodes.Status409Conflict,
            ApiErrorCodes.VideoNotReady
        },
        {
            new DomainValidationException("Value must be at least 1 seconds.", ApiErrorCodes.InvalidSpliceValue),
            StatusCodes.Status400BadRequest,
            ApiErrorCodes.InvalidSpliceValue
        },
        {
            new MediaProcessingException("A segment came out larger than the size limit."),
            StatusCodes.Status422UnprocessableEntity,
            ApiErrorCodes.MediaProcessingFailed
        },
    };

    [Theory]
    [MemberData(nameof(DomainExceptions))]
    public async Task TryHandleAsync_DomainException_WritesExpectedStatusAndApiErrorAndLogsWarning(
        DinoSplicerException exception,
        int expectedStatus,
        string expectedCode
    )
    {
        RecordingLogger<DomainExceptionHandler> logger = new();
        DomainExceptionHandler handler = new(logger);
        (DefaultHttpContext context, MemoryStream body) = CreateHttpContext();

        bool handled = await handler.TryHandleAsync(context, exception, CancellationToken.None);

        handled.Should().BeTrue();
        context.Response.StatusCode.Should().Be(expectedStatus);
        ApiError error = ReadBody(body, expectedCode);
        error.Error.Should().Be(exception.Message);
        logger.Entries.Should().ContainSingle(e => e.Level == LogLevel.Warning && e.Exception == exception);
    }

    [Fact]
    public async Task TryHandleAsync_ServerDomainException_LogsAtErrorLevel()
    {
        RecordingLogger<DomainExceptionHandler> logger = new();
        DomainExceptionHandler handler = new(logger);
        (DefaultHttpContext context, MemoryStream body) = CreateHttpContext();
        ServerDomainException exception = new("Database unavailable.", ApiErrorCodes.DatabaseError);

        bool handled = await handler.TryHandleAsync(context, exception, CancellationToken.None);

        handled.Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        ApiError error = ReadBody(body, ApiErrorCodes.DatabaseError);
        error.Error.Should().Be("Database unavailable.");
        logger.Entries.Should().ContainSingle(e => e.Level == LogLevel.Error && e.Exception == exception);
    }

    [Fact]
    public async Task TryHandleAsync_BadHttpRequestException_UsesExceptionStatusAndLogsWarning()
    {
        RecordingLogger<DomainExceptionHandler> logger = new();
        DomainExceptionHandler handler = new(logger);
        (DefaultHttpContext context, MemoryStream body) = CreateHttpContext();
        BadHttpRequestException exception = new("Request body too large", StatusCodes.Status413PayloadTooLarge);

        bool handled = await handler.TryHandleAsync(context, exception, CancellationToken.None);

        handled.Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status413PayloadTooLarge);
        ReadBody(body, ApiErrorCodes.RequestFailed);
        logger.Entries.Should().ContainSingle(e => e.Level == LogLevel.Warning && e.Exception == exception);
    }

    [Fact]
    public async Task TryHandleAsync_ClientCancelledRequest_LogsInformationWithoutWritingErrorResponse()
    {
        RecordingLogger<DomainExceptionHandler> logger = new();
        DomainExceptionHandler handler = new(logger);
        (DefaultHttpContext context, MemoryStream body) = CreateHttpContext();
        using CancellationTokenSource abortedCts = new();
        await abortedCts.CancelAsync();
        context.RequestAborted = abortedCts.Token;

        bool handled = await handler.TryHandleAsync(context, new OperationCanceledException(abortedCts.Token), CancellationToken.None);

        handled.Should().BeTrue();
        body.Length.Should().Be(0);
        logger.Entries.Should().ContainSingle(e => e.Level == LogLevel.Information && e.Message == "Request was cancelled by the client");
    }

    [Fact]
    public async Task TryHandleAsync_UnexpectedException_WritesSafe500ApiErrorWithoutLeakingDetailsAndLogsError()
    {
        RecordingLogger<DomainExceptionHandler> logger = new();
        DomainExceptionHandler handler = new(logger);
        (DefaultHttpContext context, MemoryStream body) = CreateHttpContext();
        InvalidOperationException exception = new("sensitive internal failure details");

        bool handled = await handler.TryHandleAsync(context, exception, CancellationToken.None);

        handled.Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        ApiError error = ReadBody(body, ApiErrorCodes.InternalError);
        error.Error.Should().Be("An unexpected error occurred.");
        error.Error.Should().NotContain("sensitive");
        logger.Entries.Should().ContainSingle(e => e.Level == LogLevel.Error && e.Exception == exception);
    }

    private static (DefaultHttpContext Context, MemoryStream Body) CreateHttpContext()
    {
        ServiceCollection services = new();
        services.AddLogging();
        DefaultHttpContext context = new()
        {
            RequestServices = services.BuildServiceProvider(),
        };
        MemoryStream body = new();
        context.Response.Body = body;
        return (context, body);
    }

    private static ApiError ReadBody(MemoryStream body, string expectedCode)
    {
        body.Position = 0;
        using StreamReader reader = new(body);
        string json = reader.ReadToEnd();
        return ApiErrorAssertions.ShouldBeApiErrorJson(json, expectedCode);
    }

    private sealed class ServerDomainException(string message, string errorCode)
        : DinoSplicerException(message, errorCode, StatusCodes.Status500InternalServerError);

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, Exception? Exception, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter
        )
        {
            Entries.Add((logLevel, exception, formatter(state, exception)));
        }
    }
}
