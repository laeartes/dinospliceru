using System.Collections.Concurrent;

namespace DinoSplicer.Api.Services;

public class JobProgressTracker : IJobProgressTracker
{
    private readonly ConcurrentDictionary<Guid, int> _progressByJobId = new();

    public void ReportProgress(Guid jobId, int progress)
    {
        int clamped = Math.Clamp(progress, 0, 100);
        _ = _progressByJobId.AddOrUpdate(
            jobId,
            clamped,
            (_, current) => Math.Max(current, clamped)
        );
    }

    public int? GetProgress(Guid jobId)
    {
        return _progressByJobId.TryGetValue(jobId, out int progress) ? progress : null;
    }

    public bool Remove(Guid jobId)
    {
        return _progressByJobId.TryRemove(jobId, out _);
    }
}
