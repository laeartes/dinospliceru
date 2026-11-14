using DinoSplicer.Api.Exceptions;
using DinoSplicer.Api.Models;

namespace DinoSplicer.Api.Services;

// A splice failure whose message is meant to be shown to the user
public class SpliceException(
    string message,
    string errorCode = ApiErrorCodes.MediaProcessingFailed,
    Exception? innerException = null
) : MediaProcessingException(message, errorCode, innerException);

