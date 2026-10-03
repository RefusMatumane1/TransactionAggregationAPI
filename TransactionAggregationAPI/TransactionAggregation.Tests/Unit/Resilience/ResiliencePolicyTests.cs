using BuildingBlocks.Messaging;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using SharedKernel.Exceptions;
using TransactionAggregation.Worker.Kafka;
using Xunit;

namespace TransactionAggregation.Tests.Unit.Resilience
{
    public class ResiliencePolicyTests
    {
        private static PostgresException Postgres(string sqlState) =>
            new("refused", "ERROR", "ERROR", sqlState);

        private static DbUpdateException Wrapped(string sqlState) =>
            new("An error occurred while saving the entity changes. See the inner exception for details.", Postgres(sqlState));

        [Theory]
        [InlineData("22P05")]
        [InlineData("22001")]
        [InlineData("22003")]
        [InlineData("23514")]
        [InlineData("23502")]
        public void Classifier_TreatsDataRefusalsAsPermanent(string sqlState) =>
            FailureClassifier.Classify(Wrapped(sqlState)).Should().Be(FailureKind.Permanent);

        [Theory]
        [InlineData("23505")]
        [InlineData("40001")]
        [InlineData("57P01")]
        public void Classifier_KeepsRetryableDatabaseStatesTransient(string sqlState) =>
            FailureClassifier.Classify(Wrapped(sqlState)).Should().Be(FailureKind.Transient);

        [Fact]
        public void Classifier_DomainRuleViolation_IsPermanent() =>
            FailureClassifier.Classify(new DomainException("External ID must be 1-100 characters")).Should().Be(FailureKind.Permanent);

        [Fact]
        public void Describe_ReportsTheInnermostCause_NotEfsWrapper() =>
            FailureClassifier.Describe(Wrapped("22P05")).Should().Be("PostgresException 22P05: refused");

        [Fact]
        public void Outages_AreRecognisedThroughWrappers()
        {
            FailureClassifier.IsInfrastructureOutage(new DbUpdateException("x", new TimeoutException())).Should().BeTrue();
            FailureClassifier.IsInfrastructureOutage(new HttpRequestException("connection refused")).Should().BeTrue();
            FailureClassifier.IsInfrastructureOutage(new InvalidOperationException("bug")).Should().BeFalse();
            FailureClassifier.IsInfrastructureOutage(Wrapped("22P05")).Should().BeFalse();
        }

        [Theory]
        [InlineData(0)]
        [InlineData(3)]
        [InlineData(20)]
        public void Backoff_IsJitteredBetweenHalfAndAllOfItsCappedCeiling(int attempts)
        {
            var ceiling = RetryBackoff.Ceiling(attempts);
            var random = new Random(1234);
            var delays = Enumerable.Range(0, 200).Select(_ => RetryBackoff.After(attempts, random)).ToList();

            delays.Should().OnlyContain(d => d >= ceiling / 2 && d <= ceiling);
            delays.Distinct().Should().HaveCountGreaterThan(100, "retries must not fire in lockstep across messages and replicas");
            ceiling.Should().BeLessThanOrEqualTo(TimeSpan.FromSeconds(RetryBackoff.MaxSeconds));
        }

        private sealed class SettleProbe
        {
            public int HandleCalls;
            public int DeadLetterCalls;
            public int PublishCalls;
            public List<TimeSpan> Delays { get; } = [];
            public string? DeadLetterReason;
        }

        private static Task<KafkaMessageResult> Settle(
            SettleProbe probe,
            Func<int, Task<KafkaMessageResult>> handle,
            Func<int, Task>? publish = null,
            int maxUnclassified = 3,
            CancellationToken cancellationToken = default)
        {
            var settler = new KafkaRecordSettler(
                new KafkaOptions { MaxUnclassifiedAttempts = maxUnclassified, MaxRetryBackoffSeconds = 8 }, NullLogger.Instance);

            return settler.SettleAsync(
                "topic [[0]] @0",
                _ => handle(++probe.HandleCalls),
                (reason, _) =>
                {
                    probe.DeadLetterCalls++;
                    probe.DeadLetterReason = reason;
                    return Task.FromResult(new KafkaMessageResult(KafkaMessageOutcome.Rejected, Reason: reason));
                },
                (_, _) => publish?.Invoke(++probe.PublishCalls) ?? Task.FromResult(++probe.PublishCalls),
                (delay, _) =>
                {
                    probe.Delays.Add(delay);
                    return Task.CompletedTask;
                },
                cancellationToken);
        }

        [Fact]
        public async Task Settler_PermanentFailure_IsDeadLetteredImmediately_WithoutRetrying()
        {
            var probe = new SettleProbe();

            var result = await Settle(probe, _ => throw Wrapped("22P05"));

            result.Outcome.Should().Be(KafkaMessageOutcome.Rejected);
            probe.HandleCalls.Should().Be(1);
            probe.Delays.Should().BeEmpty("waiting cannot change a refusal");
            probe.DeadLetterReason.Should().Contain("22P05");
            probe.PublishCalls.Should().Be(1);
        }

        [Fact]
        public async Task Settler_UnrecognisedFailure_IsRetriedABoundedNumberOfTimes_ThenDeadLettered()
        {
            var probe = new SettleProbe();

            var result = await Settle(probe, _ => throw new InvalidOperationException("unexpected"), maxUnclassified: 3);

            result.Outcome.Should().Be(KafkaMessageOutcome.Rejected);
            probe.HandleCalls.Should().Be(3);
            probe.Delays.Should().HaveCount(2);
            probe.DeadLetterReason.Should().Contain("Gave up after 3 attempts").And.Contain("unexpected");
        }

        [Fact]
        public async Task Settler_InfrastructureOutage_IsWaitedOut_ThenTheRecordIsStored()
        {
            var probe = new SettleProbe();

            var result = await Settle(probe, call => call < 10
                ? throw new NpgsqlException("connection refused", new System.Net.Sockets.SocketException())
                : Task.FromResult(new KafkaMessageResult(KafkaMessageOutcome.Enqueued, Guid.NewGuid())));

            result.Outcome.Should().Be(KafkaMessageOutcome.Enqueued);
            probe.HandleCalls.Should().Be(10, "an outage stalls the partition for as long as it lasts — more than the unclassified cap");
            probe.DeadLetterCalls.Should().Be(0);
            probe.Delays.Should().OnlyContain(d => d <= TimeSpan.FromSeconds(8), "backoff is capped");
        }

        [Fact]
        public async Task Settler_DeadLetterPublishFailure_RetriesOnlyThePublish_NotTheAudit()
        {
            var probe = new SettleProbe();

            var result = await Settle(
                probe,
                _ => Task.FromResult(new KafkaMessageResult(KafkaMessageOutcome.Rejected, Reason: "invalid")),
                publish: call => call < 3 ? throw new TimeoutException("broker") : Task.CompletedTask);

            result.Outcome.Should().Be(KafkaMessageOutcome.Rejected);
            probe.HandleCalls.Should().Be(1, "the rejection was already recorded");
            probe.PublishCalls.Should().Be(3);
        }

        [Fact]
        public async Task Settler_Shutdown_StopsWaitingImmediately()
        {
            using var cts = new CancellationTokenSource();
            var probe = new SettleProbe();

            var act = () => Settle(probe, _ =>
            {
                cts.Cancel();
                throw new OperationCanceledException(cts.Token);
            }, cancellationToken: cts.Token);

            await act.Should().ThrowAsync<OperationCanceledException>();
            probe.DeadLetterCalls.Should().Be(0, "an interrupted record is redelivered after restart, never dead-lettered");
        }
    }
}