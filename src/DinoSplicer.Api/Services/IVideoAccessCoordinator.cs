namespace DinoSplicer.Api.Services;

public interface IVideoAccessCoordinator
{
    /// <summary>
    /// Takes a shared lease so a worker can read the video file. Returns null if a delete is in progress.
    /// Dispose the lease when the worker is done with the file.
    /// </summary>
    IDisposable? TryAcquireProcessingLease(Guid videoId);

    /// <summary>
    /// Takes exclusive access for deletion. Returns false if any lease or another delete is active.
    /// Every successful call must be paired with <see cref="CompleteDelete"/> in a finally block.
    /// </summary>
    bool TryAcquireDelete(Guid videoId);

    void CompleteDelete(Guid videoId);
}
