using DinoSplicer.Api.Models;

namespace DinoSplicer.Api.Services;

public interface ICutRequestValidator
{
    // Throws DomainValidationException when the mode or time range is not acceptable for this video
    void Validate(CutRequest request, Video video);
}
