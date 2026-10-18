namespace DinoSplicer.Api.Models;

// Error shape: { "error": "...", "code": "..." }
public record ApiError(string Error, string Code)
{
    public static ApiError ForStatusCode(int statusCode)
    {
        return statusCode switch
        {
            StatusCodes.Status404NotFound => new ApiError("The requested resource was not found.", ApiErrorCodes.NotFound),
            StatusCodes.Status405MethodNotAllowed => new ApiError("This HTTP method is not allowed for this endpoint.", ApiErrorCodes.MethodNotAllowed),
            StatusCodes.Status415UnsupportedMediaType => new ApiError("The request content type is not supported by this endpoint.", ApiErrorCodes.UnsupportedMediaType),
            >= StatusCodes.Status500InternalServerError => new ApiError("An unexpected error occurred.", ApiErrorCodes.InternalError),
            _ => new ApiError($"The request failed with status code {statusCode}.", ApiErrorCodes.RequestFailed),
        };
    }
}
