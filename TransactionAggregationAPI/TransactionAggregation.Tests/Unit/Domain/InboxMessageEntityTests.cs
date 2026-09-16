using FluentAssertions;
using TransactionAggregation.Domain.Inbox;
using Xunit;

namespace TransactionAggregation.Tests.Unit.Domain;

public class InboxMessageEntityTests
{
    // ── Create ────────────────────────────────────────────────────────────────

    [Fact]
    public void Create_SetsPendingStatusWithZeroAttempts()
    {
        var message = InboxMessage.Create("stitch", "{}");

        message.SourceName.Should().Be("stitch");
        message.Payload.Should().Be("{}");
        message.Status.Should().Be(InboxMessageStatus.Pending);
        message.Attempts.Should().Be(0);
        message.ProcessedAt.Should().BeNull();
        message.ClaimedAt.Should().BeNull();
        message.LastError.Should().BeNull();
    }

    // ── MarkProcessed ─────────────────────────────────────────────────────────

    [Fact]
    public void MarkProcessed_SetsStatusAndProcessedAtAndClearsClaimedAt()
    {
        var message = InboxMessage.Create("stitch", "{}");

        message.MarkProcessed();

        message.Status.Should().Be(InboxMessageStatus.Processed);
        message.ProcessedAt.Should().NotBeNull();
        message.ClaimedAt.Should().BeNull();
    }

    // ── MarkFailed ────────────────────────────────────────────────────────────

    [Fact]
    public void MarkFailed_UnderMaxAttempts_StaysPendingWithBackoffAndIncrementsAttempts()
    {
        var message = InboxMessage.Create("stitch", "{}");

        message.MarkFailed("boom", TimeSpan.FromSeconds(30), maxAttempts: 5);

        message.Status.Should().Be(InboxMessageStatus.Pending);
        message.Attempts.Should().Be(1);
        message.LastError.Should().Be("boom");
        message.NextAttemptAt.Should().NotBeNull();
        message.NextAttemptAt!.Value.Should().BeAfter(DateTime.UtcNow);
        message.ClaimedAt.Should().BeNull();
    }

    [Fact]
    public void MarkFailed_AtMaxAttempts_DeadLetters()
    {
        var message = InboxMessage.Create("stitch", "{}");

        for (var i = 0; i < 4; i++)
            message.MarkFailed("boom", TimeSpan.FromSeconds(1), maxAttempts: 5);

        message.Status.Should().Be(InboxMessageStatus.Pending);

        message.MarkFailed("boom", TimeSpan.FromSeconds(1), maxAttempts: 5);

        message.Status.Should().Be(InboxMessageStatus.DeadLettered);
        message.Attempts.Should().Be(5);
    }
}
