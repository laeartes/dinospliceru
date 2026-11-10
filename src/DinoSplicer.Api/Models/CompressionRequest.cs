using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace DinoSplicer.Api.Models;

// Every field is optional
public class CompressionRequest
{
    public const int DefaultCrf = 23;

    [JsonConverter(typeof(JsonStringEnumConverter<OutputContainerFormat>))]
    [EnumDataType(typeof(OutputContainerFormat))]
    public OutputContainerFormat Format { get; set; } = OutputContainerFormat.Mp4;

    [JsonConverter(typeof(JsonStringEnumConverter<VideoCodecOption>))]
    [EnumDataType(typeof(VideoCodecOption))]
    public VideoCodecOption VideoCodec { get; set; } = VideoCodecOption.H264;

    [Range(CompressionJob.MinCrf, CompressionJob.MaxCrf)]
    public int Crf { get; set; } = DefaultCrf;

    // null keeps source resolution
    [Range(1, int.MaxValue)]
    public int? TargetWidth { get; set; }

    [Range(1, int.MaxValue)]
    public int? TargetHeight { get; set; }

    // null keeps source audio bitrate
    [Range(CompressionJob.MinAudioBitrateKbps, CompressionJob.MaxAudioBitrateKbps)]
    public int? AudioBitrateKbps { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter<EncodingPresetOption>))]
    [EnumDataType(typeof(EncodingPresetOption))]
    public EncodingPresetOption Preset { get; set; } = EncodingPresetOption.Medium;
}
