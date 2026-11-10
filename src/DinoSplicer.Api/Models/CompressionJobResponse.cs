namespace DinoSplicer.Api.Models;

public record CompressionJobResponse(
    Guid JobId,
    string Format,
    string VideoCodec,
    int Crf,
    int? TargetWidth,
    int? TargetHeight,
    int? AudioBitrateKbps,
    string Preset,
    string Status,
    int? ProgressPercent,
    string? Error,
    string? OutputFileName,
    double? OutputDurationSeconds,
    long? OutputSizeBytes
)
{
    public static CompressionJobResponse FromJob(CompressionJob job, int? progressPercent = null)
    {
        return new CompressionJobResponse(
            job.Id,
            job.Format.ToString(),
            job.VideoCodec.ToString(),
            job.Crf,
            job.TargetWidth,
            job.TargetHeight,
            job.AudioBitrateKbps,
            job.Preset.ToString(),
            job.Status.ToString(),
            progressPercent,
            job.ErrorMessage,
            job.OutputFileName,
            job.OutputDurationSeconds,
            job.OutputSizeBytes
        );
    }
}