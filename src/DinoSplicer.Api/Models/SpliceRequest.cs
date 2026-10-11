using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace DinoSplicer.Api.Models;

public class SpliceRequest
{
    [JsonConverter(typeof(JsonStringEnumConverter<SpliceMode>))]
    [EnumDataType(typeof(SpliceMode))]
    public SpliceMode Mode { get; set; }

    // Seconds for ByLength, bytes for BySize
    [Range(0.0, double.MaxValue, MinimumIsExclusive = true)]
    public double Value { get; set; }
}
