using System.Collections.Concurrent;

namespace DinoSplicer.Api.Services;

public class VideoAccessCoordinator : IVideoAccessCoordinator
{
    private readonly ConcurrentDictionary<Guid, VideoLockEntry> _entries = new();

    public IDisposable? TryAcquireProcessingLease(Guid videoId)
    {
        while (true)
        {
            VideoLockEntry entry = _entries.GetOrAdd(videoId, static _ => new VideoLockEntry());

            lock (entry.SyncRoot)
            {
                if (entry.IsEvicted)
                {
                    continue;
                }

                if (entry.IsDeleting)
                {
                    return null;
                }

                entry.ActiveReaders++;
                return new ProcessingLease(this, videoId, entry);
            }
        }
    }

    public bool TryAcquireDelete(Guid videoId)
    {
        while (true)
        {
            VideoLockEntry entry = _entries.GetOrAdd(videoId, static _ => new VideoLockEntry());

            lock (entry.SyncRoot)
            {
                if (entry.IsEvicted)
                {
                    continue;
                }

                if ((entry.ActiveReaders > 0) || entry.IsDeleting)
                {
                    return false;
                }

                entry.IsDeleting = true;
                return true;
            }
        }
    }

    public void CompleteDelete(Guid videoId)
    {
        if (!_entries.TryGetValue(videoId, out VideoLockEntry? entry))
        {
            return;
        }

        lock (entry.SyncRoot)
        {
            if (entry.IsEvicted)
            {
                return;
            }

            entry.IsDeleting = false;
            if (entry.ActiveReaders == 0)
            {
                entry.IsEvicted = true;
                _ = _entries.TryRemove(KeyValuePair.Create(videoId, entry));
            }
        }
    }

    private void ReleaseProcessingLease(Guid videoId, VideoLockEntry entry)
    {
        lock (entry.SyncRoot)
        {
            entry.ActiveReaders--;
            if ((entry.ActiveReaders == 0) && !entry.IsDeleting && !entry.IsEvicted)
            {
                entry.IsEvicted = true;
                _ = _entries.TryRemove(KeyValuePair.Create(videoId, entry));
            }
        }
    }

    private sealed class VideoLockEntry
    {
        public object SyncRoot { get; } = new();

        public int ActiveReaders { get; set; }

        public bool IsDeleting { get; set; }

        public bool IsEvicted { get; set; }
    }

    private sealed class ProcessingLease(VideoAccessCoordinator coordinator, Guid videoId, VideoLockEntry entry) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                coordinator.ReleaseProcessingLease(videoId, entry);
            }
        }
    }
}
