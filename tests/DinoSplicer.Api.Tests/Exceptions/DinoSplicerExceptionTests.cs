using DinoSplicer.Api.Exceptions;
using DinoSplicer.Api.Models;

using FluentAssertions;

using Microsoft.AspNetCore.Http;

namespace DinoSplicer.Api.Tests.Exceptions;

public class DinoSplicerExceptionTests
{
    [Fact]
    public void ResourceNotFoundException_Constructor_Sets404StatusAndErrorCode()
    {
        ResourceNotFoundException exception = new("Video missing", ApiErrorCodes.VideoNotFound);

        exception.Message.Should().Be("Video missing");
        exception.ErrorCode.Should().Be(ApiErrorCodes.VideoNotFound);
        exception.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        exception.Should().BeAssignableTo<DinoSplicerException>();
    }

    [Fact]
    public void ResourceConflictException_Constructor_Sets409StatusAndErrorCode()
    {
        ResourceConflictException exception = new("Video not ready", ApiErrorCodes.VideoNotReady);

        exception.Message.Should().Be("Video not ready");
        exception.ErrorCode.Should().Be(ApiErrorCodes.VideoNotReady);
        exception.StatusCode.Should().Be(StatusCodes.Status409Conflict);
        exception.Should().BeAssignableTo<DinoSplicerException>();
    }

    [Fact]
    public void DomainValidationException_Constructor_Sets400StatusAndDefaultOrCustomErrorCode()
    {
        DomainValidationException defaultCode = new("Invalid request");
        DomainValidationException customCode = new("Invalid splice value", ApiErrorCodes.InvalidSpliceValue);

        defaultCode.Message.Should().Be("Invalid request");
        defaultCode.ErrorCode.Should().Be(ApiErrorCodes.ValidationFailed);
        defaultCode.StatusCode.Should().Be(StatusCodes.Status400BadRequest);

        customCode.ErrorCode.Should().Be(ApiErrorCodes.InvalidSpliceValue);
        customCode.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public void MediaProcessingException_Constructor_Sets422StatusAndDefaultOrCustomErrorCode()
    {
        Exception inner = new InvalidOperationException("ffmpeg error");
        MediaProcessingException exception = new("Cannot cut video", innerException: inner);

        exception.Message.Should().Be("Cannot cut video");
        exception.ErrorCode.Should().Be(ApiErrorCodes.MediaProcessingFailed);
        exception.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
        exception.InnerException.Should().BeSameAs(inner);
        exception.Should().BeAssignableTo<DinoSplicerException>();
    }
}
