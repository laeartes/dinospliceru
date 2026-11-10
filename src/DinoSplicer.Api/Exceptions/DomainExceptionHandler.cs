using DinoSplicer.Api.Models;

using Microsoft.AspNetCore.Diagnostics;

namespace DinoSplicer.Api.Exceptions;

public class DomainExceptionHandler(ILogger<DomainExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken
    )
    {
        int statusCode;
        ApiError response;

        if (exception is DinoSplicerException domainException)
        {
            statusCode = domainException.StatusCode;
            response = new ApiError(domainException.Message, domainException.ErrorCode);

            if (statusCode >= StatusCodes.Status500InternalServerError)
            {
                logger.LogError(
                    domainException,
                    "Domain error {ErrorCode} failed with status {StatusCode}",
                    domainException.ErrorCode,
                    statusCode
                );
            }
            else
            {
                logger.LogWarning(
                    domainException,
                    "Domain error {ErrorCode} handled with status {StatusCode}: {Message}",
                    domainException.ErrorCode,
                    statusCode,
                    domainException.Message
                );
            }
        }
        else if (exception is BadHttpRequestException badRequest)
        {
            statusCode = badRequest.StatusCode;
            response = ApiError.ForStatusCode(statusCode);
            logger.LogWarning(badRequest, "Bad HTTP request handled with status {StatusCode}", statusCode);
        }
        else if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            logger.LogInformation("Request was cancelled by the client");
            return true;
        }
        else
        {
            statusCode = StatusCodes.Status500InternalServerError;
            response = ApiError.ForStatusCode(statusCode);
            logger.LogError(exception, "Unhandled exception while processing request");
        }

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(response, cancellationToken);
        return true;
    }
}
