namespace DinoSplicer.Api.Models;

public record SpliceJobResponse(Guid JobId, string Status)
{
    public static SpliceJobResponse FromJob(SpliceJob job) => new(job.Id, job.Status.ToString());
}
