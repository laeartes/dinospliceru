using DinoSplicer.Api.Models;

namespace DinoSplicer.Api.Exceptions;

// A media-processing failure whose message is meant to be shown to the user
public class MediaProcessingException(
    string message,
    string errorCode = ApiErrorCodes.MediaProcessingFailed,
    Exception? innerException = null
) : DinoSplicerException(message, errorCode, StatusCodes.Status422UnprocessableEntity, innerException);
