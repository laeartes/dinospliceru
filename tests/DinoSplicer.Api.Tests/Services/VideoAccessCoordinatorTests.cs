using DinoSplicer.Api.Services;

using FluentAssertions;

namespace DinoSplicer.Api.Tests.Services;

public class VideoAccessCoordinatorTests
{
    [Fact]
    public void TryAcquireProcessingLease_MultipleConcurrentReaders_AllowsAllAndBlocksDeleteUntilAllDisposed()
    {
        VideoAccessCoordinator coordinator = new();
        Guid videoId = Guid.NewGuid();

        IDisposable? lease1 = coordinator.TryAcquireProcessingLease(videoId);
        IDisposable? lease2 = coordinator.TryAcquireProcessingLease(videoId);

        lease1.Should().NotBeNull();
        lease2.Should().NotBeNull();

        coordinator.TryAcquireDelete(videoId).Should().BeFalse();

        lease1!.Dispose();
        coordinator.TryAcquireDelete(videoId).Should().BeFalse();

        lease2!.Dispose();
        coordinator.TryAcquireDelete(videoId).Should().BeTrue();

        coordinator.CompleteDelete(videoId);
    }

    [Fact]
    public void Dispose_CalledMultipleTimesOnSameLease_DecrementsReaderCountOnlyOnce()
    {
        VideoAccessCoordinator coordinator = new();
        Guid videoId = Guid.NewGuid();

        using IDisposable? lease1 = coordinator.TryAcquireProcessingLease(videoId);
        using IDisposable? lease2 = coordinator.TryAcquireProcessingLease(videoId);

        lease1!.Dispose();
        lease1.Dispose();
        lease1.Dispose();

        // lease2 is still active, so delete must still be blocked
        coordinator.TryAcquireDelete(videoId).Should().BeFalse();

        lease2!.Dispose();
        coordinator.TryAcquireDelete(videoId).Should().BeTrue();
        coordinator.CompleteDelete(videoId);
    }

    [Fact]
    public void TryAcquireDelete_WhileDeleteIsActive_BlocksNewReadersAndConcurrentDeletesUntilCompleted()
    {
        VideoAccessCoordinator coordinator = new();
        Guid videoId = Guid.NewGuid();

        coordinator.TryAcquireDelete(videoId).Should().BeTrue();

        coordinator.TryAcquireProcessingLease(videoId).Should().BeNull();
        coordinator.TryAcquireDelete(videoId).Should().BeFalse();

        coordinator.CompleteDelete(videoId);

        using IDisposable? leaseAfterComplete = coordinator.TryAcquireProcessingLease(videoId);
        leaseAfterComplete.Should().NotBeNull();
    }

    [Fact]
    public void TryAcquireDelete_IndependentVideos_DoNotBlockEachOther()
    {
        VideoAccessCoordinator coordinator = new();
        Guid videoA = Guid.NewGuid();
        Guid videoB = Guid.NewGuid();

        using IDisposable? leaseA = coordinator.TryAcquireProcessingLease(videoA);

        coordinator.TryAcquireDelete(videoA).Should().BeFalse();
        coordinator.TryAcquireDelete(videoB).Should().BeTrue();

        coordinator.CompleteDelete(videoB);
    }

    [Fact]
    public async Task ConcurrentReaderLeasesAndDeletes_UnderHighContention_NeverOverlapReaderAndDelete()
    {
        VideoAccessCoordinator coordinator = new();
        Guid videoId = Guid.NewGuid();
        const int readerTasks = 32;
        const int deleteTasks = 8;
        const int iterations = 200;

        int activeReaders = 0;
        int activeDeleters = 0;
        bool overlapDetected = false;

        using ManualResetEventSlim startGate = new(false);

        Task[] readers = Enumerable.Range(0, readerTasks).Select(_ => Task.Run(() =>
        {
            startGate.Wait();
            for (int i = 0; i < iterations; i++)
            {
                using IDisposable? lease = coordinator.TryAcquireProcessingLease(videoId);
                if (lease is null)
                {
                    continue;
                }

                Interlocked.Increment(ref activeReaders);
                if (Volatile.Read(ref activeDeleters) > 0)
                {
                    Volatile.Write(ref overlapDetected, true);
                }

                Thread.SpinWait(50);

                if (Volatile.Read(ref activeDeleters) > 0)
                {
                    Volatile.Write(ref overlapDetected, true);
                }

                Interlocked.Decrement(ref activeReaders);
            }
        })).ToArray();

        Task[] deleters = Enumerable.Range(0, deleteTasks).Select(_ => Task.Run(() =>
        {
            startGate.Wait();
            for (int i = 0; i < iterations; i++)
            {
                if (!coordinator.TryAcquireDelete(videoId))
                {
                    continue;
                }

                try
                {
                    if (Interlocked.Increment(ref activeDeleters) > 1)
                    {
                        Volatile.Write(ref overlapDetected, true);
                    }

                    if (Volatile.Read(ref activeReaders) > 0)
                    {
                        Volatile.Write(ref overlapDetected, true);
                    }

                    Thread.SpinWait(50);

                    if (Volatile.Read(ref activeReaders) > 0)
                    {
                        Volatile.Write(ref overlapDetected, true);
                    }
                }
                finally
                {
                    Interlocked.Decrement(ref activeDeleters);
                    coordinator.CompleteDelete(videoId);
                }
            }
        })).ToArray();

        startGate.Set();
        await Task.WhenAll([.. readers, .. deleters]);

        overlapDetected.Should().BeFalse();
        coordinator.TryAcquireDelete(videoId).Should().BeTrue();
        coordinator.CompleteDelete(videoId);
    }
}
