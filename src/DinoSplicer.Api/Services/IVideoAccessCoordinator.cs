namespace DinoSplicer.Api.Services;

public interface IVideoAccessCoordinator
{
    IDisposable? TryAcquireProcessingLease(Guid videoId);

    bool TryAcquireDelete(Guid videoId);

    void CompleteDelete(Guid videoId);
}
