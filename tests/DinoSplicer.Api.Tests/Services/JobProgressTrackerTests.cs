using DinoSplicer.Api.Services;

using FluentAssertions;

namespace DinoSplicer.Api.Tests.Services;

public class JobProgressTrackerTests
{
    [Fact]
    public void GetProgress_UntrackedJob_ReturnsNull()
    {
        JobProgressTracker tracker = new();

        int? progress = tracker.GetProgress(Guid.NewGuid());

        progress.Should().BeNull();
    }

    [Theory]
    [InlineData(-50, 0)]
    [InlineData(-1, 0)]
    [InlineData(0, 0)]
    [InlineData(42, 42)]
    [InlineData(100, 100)]
    [InlineData(101, 100)]
    [InlineData(500, 100)]
    public void ReportProgress_ValueOutsideOrWithinBounds_ClampsToZeroThroughHundred(int reported, int expected)
    {
        JobProgressTracker tracker = new();
        Guid jobId = Guid.NewGuid();

        tracker.ReportProgress(jobId, reported);

        tracker.GetProgress(jobId).Should().Be(expected);
    }

    [Fact]
    public void ReportProgress_LowerValueAfterHigherValue_DoesNotRegressProgress()
    {
        JobProgressTracker tracker = new();
        Guid jobId = Guid.NewGuid();

        tracker.ReportProgress(jobId, 65);
        tracker.ReportProgress(jobId, 30);
        tracker.ReportProgress(jobId, -10);

        tracker.GetProgress(jobId).Should().Be(65);
    }

    [Fact]
    public void Remove_TrackedAndUntrackedJob_RemovesEntryAndReturnsWhetherItExisted()
    {
        JobProgressTracker tracker = new();
        Guid jobId = Guid.NewGuid();
        tracker.ReportProgress(jobId, 50);

        bool firstRemove = tracker.Remove(jobId);
        bool secondRemove = tracker.Remove(jobId);

        firstRemove.Should().BeTrue();
        secondRemove.Should().BeFalse();
        tracker.GetProgress(jobId).Should().BeNull();
    }

    [Fact]
    public async Task ReportAndGetProgress_ConcurrentUpdatesAndReadsAcrossDozensOfTasks_NeverThrowsOrRegresses()
    {
        JobProgressTracker tracker = new();
        Guid jobId = Guid.NewGuid();
        const int workerCount = 64;
        const int iterationsPerWorker = 250;

        using ManualResetEventSlim startGate = new(false);

        Task[] writers = Enumerable.Range(0, workerCount).Select(workerIndex => Task.Run(() =>
        {
            startGate.Wait();
            Random rng = new(workerIndex * 397);
            for (int i = 0; i < iterationsPerWorker; i++)
            {
                int candidate = rng.Next(-25, 125);
                tracker.ReportProgress(jobId, candidate);
            }
        })).ToArray();

        bool regressionObserved = false;
        bool outOfBoundsObserved = false;

        Task[] readers = Enumerable.Range(0, 16).Select(_ => Task.Run(() =>
        {
            startGate.Wait();
            int lastSeen = -1;
            for (int i = 0; i < (iterationsPerWorker * 4); i++)
            {
                int? current = tracker.GetProgress(jobId);
                if (current is int value)
                {
                    if ((value < 0) || (value > 100))
                    {
                        Volatile.Write(ref outOfBoundsObserved, true);
                    }

                    if (value < lastSeen)
                    {
                        Volatile.Write(ref regressionObserved, true);
                    }

                    lastSeen = value;
                }
            }
        })).ToArray();

        startGate.Set();
        await Task.WhenAll([.. writers, .. readers]);

        tracker.ReportProgress(jobId, 100);

        outOfBoundsObserved.Should().BeFalse();
        regressionObserved.Should().BeFalse();
        tracker.GetProgress(jobId).Should().Be(100);
    }
}
