using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace DinoSplicer.Api.Models;

public class CutRequest : IValidatableObject
{
    [Range(0.0, double.MaxValue)]
    public double StartSeconds { get; set; }

    [Range(0.0, double.MaxValue, MinimumIsExclusive = true)]
    public double EndSeconds { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter<CutMode>))]
    [EnumDataType(typeof(CutMode))]
    public CutMode Mode { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (EndSeconds <= StartSeconds)
        {
            yield return new ValidationResult(
                "EndSeconds must be greater than StartSeconds.",
                [nameof(EndSeconds)]
            );
        }
    }
}
