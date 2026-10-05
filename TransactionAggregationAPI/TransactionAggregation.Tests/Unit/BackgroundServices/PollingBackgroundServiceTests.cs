using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using TransactionAggregation.Worker.BackgroundServices;
using Xunit;

namespace TransactionAggregation.Tests.Unit.BackgroundServices
{
    public class PollingBackgroundServiceTests
    {
        private sealed class FlakyJob(Exception firstFailure) : PollingBackgroundService(NullLogger.Instance)
        {
            private int _runs;
            public TaskCompletionSource SecondRun { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

            protected override TimeSpan Interval => TimeSpan.FromMilliseconds(10);

            protected override Task<bool> RunOnceAsync(CancellationToken cancellationToken)
            {
                if (Interlocked.Increment(ref _runs) == 1)
                    throw firstFailure;

                SecondRun.TrySetResult();
                return Task.FromResult(false);
            }
        }

        private sealed class BacklogJob(int fullBatches) : PollingBackgroundService(NullLogger.Instance)
        {
            private int _runs;
            public TaskCompletionSource Drained { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

            protected override TimeSpan Interval => TimeSpan.FromMinutes(5);

            protected override Task<bool> RunOnceAsync(CancellationToken cancellationToken)
            {
                var run = Interlocked.Increment(ref _runs);
                if (run > fullBatches)
                    Drained.TrySetResult();
                return Task.FromResult(run <= fullBatches);
            }
        }

        [Fact]
        public async Task AFullBatch_RunsAgainAtOnce_InsteadOfWaitingForTheInterval()
        {
            var job = new BacklogJob(fullBatches: 3);

            await job.StartAsync(CancellationToken.None);
            var completed = await Task.WhenAny(job.Drained.Task, Task.Delay(TimeSpan.FromSeconds(5)));
            await job.StopAsync(CancellationToken.None);

            completed.Should().Be(job.Drained.Task, "a backlog is drained back to back, not one batch per five-minute interval");
        }

        [Fact]
        public async Task FailedRun_IsRetriedOnTheNextInterval()
        {
            var job = new FlakyJob(new InvalidOperationException("database unavailable"));

            await job.StartAsync(CancellationToken.None);
            var completed = await Task.WhenAny(job.SecondRun.Task, Task.Delay(TimeSpan.FromSeconds(5)));
            await job.StopAsync(CancellationToken.None);

            completed.Should().Be(job.SecondRun.Task);
        }

        [Fact]
        public async Task CancellationThatIsNotShutdown_DoesNotStopTheLoop()
        {
            var job = new FlakyJob(new TaskCanceledException("command timed out"));

            await job.StartAsync(CancellationToken.None);
            var completed = await Task.WhenAny(job.SecondRun.Task, Task.Delay(TimeSpan.FromSeconds(5)));
            await job.StopAsync(CancellationToken.None);

            completed.Should().Be(job.SecondRun.Task);
        }
    }
}