using DinoSplicer.Api.Models;

namespace DinoSplicer.Api.Services;

public interface ICompressionRequestValidator
{
    // Throws DomainValidationException when compression parameters or source video constraints are invalid
    void Validate(CompressionRequest request, Video video);
}
