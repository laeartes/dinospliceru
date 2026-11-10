using System.ComponentModel.DataAnnotations;

namespace DinoSplicer.Api.Models;

public class CompressionJob
{
    public const string OutputDirectoryName = "compressions";

    public const int MinCrf = 0;
    public const int MaxCrf = 63;
    public const int MinAudioBitrateKbps = 32;
    public const int MaxAudioBitrateKbps = 512;

    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid SourceVideoId { get; set; }

    public Video? SourceVideo { get; set; }

    [EnumDataType(typeof(OutputContainerFormat))]
    public OutputContainerFormat Format { get; set; }

    [EnumDataType(typeof(VideoCodecOption))]
    public VideoCodecOption VideoCodec { get; set; }

    [Range(MinCrf, MaxCrf)]
    public int Crf { get; set; }

    // null keeps source resolution
    [Range(1, int.MaxValue)]
    public int? TargetWidth { get; set; }

    [Range(1, int.MaxValue)]
    public int? TargetHeight { get; set; }

    // null keeps source audio bitrate
    [Range(MinAudioBitrateKbps, MaxAudioBitrateKbps)]
    public int? AudioBitrateKbps { get; set; }

    [EnumDataType(typeof(EncodingPresetOption))]
    public EncodingPresetOption Preset { get; set; }

    [EnumDataType(typeof(CompressionJobStatus))]
    public CompressionJobStatus Status { get; set; } = CompressionJobStatus.Pending;

    [MaxLength(255)]
    public string? OutputFileName { get; set; }

    [MaxLength(1024)]
    public string? OutputStoragePath { get; set; }

    [Range(0.0, double.MaxValue)]
    public double? OutputDurationSeconds { get; set; }

    [Range(0, long.MaxValue)]
    public long? OutputSizeBytes { get; set; }

    [MaxLength(500)]
    public string? ErrorMessage { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public static string GetOutputDirectory(string storageDirectory, Guid jobId)
    {
        return Path.Combine(storageDirectory, OutputDirectoryName, jobId.ToString());
    }
}
