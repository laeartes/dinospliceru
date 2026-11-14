namespace DinoSplicer.Api.Exceptions;

public abstract class DinoSplicerException(
    string message,
    string errorCode,
    int statusCode,
    Exception? innerException = null
) : Exception(message, innerException)
{
    public string ErrorCode { get; } = errorCode;

    public int StatusCode { get; } = statusCode;
}
