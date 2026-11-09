namespace DinoSplicer.Api.Services;

public interface IJobProgressTracker
{
    void ReportProgress(Guid jobId, int progress);

    int? GetProgress(Guid jobId);

    bool Remove(Guid jobId);
}
