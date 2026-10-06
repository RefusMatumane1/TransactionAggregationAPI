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

        private sealed class AlwaysFailingJob : PollingBackgroundService
        {
            public AlwaysFailingJob() : base(NullLogger.Instance) { }

            public int Runs;
            public List<int> FailureCounts { get; } = [];
            public TaskCompletionSource ThirdRun { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

            protected override TimeSpan Interval => TimeSpan.FromHours(1);

            protected override TimeSpan DelayAfterFailure(int consecutiveFailures)
            {
                FailureCounts.Add(consecutiveFailures);
                return TimeSpan.FromMilliseconds(5);
            }

            protected override Task<bool> RunOnceAsync(CancellationToken cancellationToken)
            {
                if (Interlocked.Increment(ref Runs) == 3)
                    ThirdRun.TrySetResult();
                throw new InvalidOperationException("database unavailable");
            }
        }

        [Fact]
        public async Task AFailingRun_WaitsTheJobsFailureDelay_CountsConsecutiveFailures_AndIsCounted()
        {
            var job = new AlwaysFailingJob();
            var before = PollingBackgroundService.FailedRuns.WithLabels(nameof(AlwaysFailingJob)).Value;

            await job.StartAsync(CancellationToken.None);
            var completed = await Task.WhenAny(job.ThirdRun.Task, Task.Delay(TimeSpan.FromSeconds(5)));
            await job.StopAsync(CancellationToken.None);

            completed.Should().Be(job.ThirdRun.Task, "the failure delay, not the one-hour interval, decides when it retries");
            job.FailureCounts.Take(2).Should().Equal(1, 2);
            PollingBackgroundService.FailedRuns.WithLabels(nameof(AlwaysFailingJob)).Value.Should().BeGreaterThanOrEqualTo(before + 2);
        }

        [Theory]
        [InlineData(1, 30)]
        [InlineData(2, 60)]
        [InlineData(3, 120)]
        [InlineData(7, 1920)]
        [InlineData(8, 3600)]
        [InlineData(50, 3600)]
        public void AFailedTotalsRefresh_BacksOffFrom30Seconds_UpToTheInterval(int failures, int expectedSeconds)
        {
            DailyTotalsRefreshBackgroundService.RetryDelay(failures, TimeSpan.FromHours(1))
                .Should().Be(TimeSpan.FromSeconds(expectedSeconds));
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