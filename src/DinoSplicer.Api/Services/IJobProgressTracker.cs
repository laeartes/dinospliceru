namespace DinoSplicer.Api.Services;

public interface IJobProgressTracker
{
    /// <summary>Stores progress clamped to 0..100. A lower value than the stored one is ignored.</summary>
    void ReportProgress(Guid jobId, int progress);

    int? GetProgress(Guid jobId);

    bool Remove(Guid jobId);
}
