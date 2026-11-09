using DinoSplicer.Api.Models;

namespace DinoSplicer.Api.Services;

public interface ISpliceRequestValidator
{
    string? Validate(SpliceMode mode, double value, Video video);
}
