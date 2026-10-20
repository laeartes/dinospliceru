namespace DinoSplicer.Api.Models;

public record SpliceJobResponse(Guid JobId, string Status, string? Error, IReadOnlyList<SpliceOutputResponse> Outputs)
{
    // The job's Outputs must be loaded, otherwise the list comes back empty
    public static SpliceJobResponse FromJob(SpliceJob job)
    {
        return new SpliceJobResponse(
            job.Id,
            job.Status.ToString(),
            job.ErrorMessage,
            job.Outputs.Order().Select(SpliceOutputResponse.FromOutput).ToList()
        );
    }
}
