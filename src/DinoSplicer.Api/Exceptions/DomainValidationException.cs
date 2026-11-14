using DinoSplicer.Api.Models;

namespace DinoSplicer.Api.Exceptions;

public class DomainValidationException(
    string message,
    string errorCode = ApiErrorCodes.ValidationFailed,
    Exception? innerException = null
) : DinoSplicerException(message, errorCode, StatusCodes.Status400BadRequest, innerException);
