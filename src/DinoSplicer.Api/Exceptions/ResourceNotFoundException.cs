namespace DinoSplicer.Api.Exceptions;

public class ResourceNotFoundException(
    string message,
    string errorCode,
    Exception? innerException = null
) : DinoSplicerException(message, errorCode, StatusCodes.Status404NotFound, innerException);
