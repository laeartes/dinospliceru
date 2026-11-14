namespace DinoSplicer.Api.Exceptions;

public class ResourceConflictException(
    string message,
    string errorCode,
    Exception? innerException = null
) : DinoSplicerException(message, errorCode, StatusCodes.Status409Conflict, innerException);
