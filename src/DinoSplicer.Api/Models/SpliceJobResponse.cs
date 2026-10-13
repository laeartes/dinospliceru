namespace DinoSplicer.Api.Models;

public record SpliceJobResponse(Guid JobId, string Status, string? Error)
{
    public static SpliceJobResponse FromJob(SpliceJob job) => new(job.Id, job.Status.ToString(), job.ErrorMessage);
}
