namespace DinoSplicer.Api.Models;

public record CutJobResponse(
    Guid JobId,
    string Mode,
    double StartSeconds,
    double EndSeconds,
    string Status,
    int? ProgressPercent,
    string? Error,
    string? OutputFileName,
    double? OutputDurationSeconds,
    long? OutputSizeBytes
)
{
    public static CutJobResponse FromJob(CutJob job, int? progressPercent = null)
    {
        return new CutJobResponse(
            job.Id,
            job.Mode.ToString(),
            job.StartSeconds,
            job.EndSeconds,
            job.Status.ToString(),
            progressPercent,
            job.ErrorMessage,
            job.OutputFileName,
            job.OutputDurationSeconds,
            job.OutputSizeBytes
        );
    }
}
