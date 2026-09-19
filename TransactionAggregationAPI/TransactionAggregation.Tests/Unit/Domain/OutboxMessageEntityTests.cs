using FluentAssertions;
using BuildingBlocks.Messaging.Outbox;
using Xunit;

namespace TransactionAggregation.Tests.Unit.Domain;

public class OutboxMessageEntityTests
{

    [Fact]
    public void Create_SetsPendingStatusWithZeroAttempts()
    {
        var message = OutboxMessage.Create("TransactionCreated", "{}");

        message.Type.Should().Be("TransactionCreated");
        message.Payload.Should().Be("{}");
        message.Status.Should().Be(OutboxMessageStatus.Pending);
        message.Attempts.Should().Be(0);
        message.ProcessedAt.Should().BeNull();
        message.ClaimedAt.Should().BeNull();
        message.LastError.Should().BeNull();
    }

    [Fact]
    public void MarkProcessed_SetsStatusAndProcessedAtAndClearsClaimedAt()
    {
        var message = OutboxMessage.Create("TransactionCreated", "{}");

        message.MarkProcessed();

        message.Status.Should().Be(OutboxMessageStatus.Processed);
        message.ProcessedAt.Should().NotBeNull();
        message.ClaimedAt.Should().BeNull();
    }

    [Fact]
    public void MarkFailed_UnderMaxAttempts_StaysPendingWithBackoffAndIncrementsAttempts()
    {
        var message = OutboxMessage.Create("TransactionCreated", "{}");

        message.MarkFailed("boom", TimeSpan.FromSeconds(30), maxAttempts: 5);

        message.Status.Should().Be(OutboxMessageStatus.Pending);
        message.Attempts.Should().Be(1);
        message.LastError.Should().Be("boom");
        message.NextAttemptAt.Should().NotBeNull();
        message.NextAttemptAt!.Value.Should().BeAfter(DateTime.UtcNow);
        message.ClaimedAt.Should().BeNull();
    }

    [Fact]
    public void MarkFailed_AtMaxAttempts_DeadLetters()
    {
        var message = OutboxMessage.Create("TransactionCreated", "{}");

        for (var i = 0; i < 4; i++)
            message.MarkFailed("boom", TimeSpan.FromSeconds(1), maxAttempts: 5);

        message.Status.Should().Be(OutboxMessageStatus.Pending);

        message.MarkFailed("boom", TimeSpan.FromSeconds(1), maxAttempts: 5);

        message.Status.Should().Be(OutboxMessageStatus.DeadLettered);
        message.Attempts.Should().Be(5);
    }
}