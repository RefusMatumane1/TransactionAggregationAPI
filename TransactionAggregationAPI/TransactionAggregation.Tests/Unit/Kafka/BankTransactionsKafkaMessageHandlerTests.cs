using FluentAssertions;
using MediatR;
using Modules.Audit.Contracts;
using Modules.Transactions.Application.Common.Inbox;
using Modules.Transactions.Application.Features.Transactions.Commands.ReceiveBankTransactions;
using Modules.Transactions.Infrastructure.Kafka;
using Modules.WebhookSources.Contracts;
using NSubstitute;
using SharedKernel.Common.Models;
using Xunit;

namespace TransactionAggregation.Tests.Unit.Kafka;

public class BankTransactionsKafkaMessageHandlerTests
{
    private const string ValidMessage = """
        {
          "externalAccountId": "ext-acc-1",
          "transactions": [
            { "id": "txn-1", "amount": -150.00, "currency": "ZAR", "description": "Woolworths", "category": null, "date": "2026-09-01T10:00:00Z" }
          ]
        }
        """;

    private static readonly KafkaOptions Options = new() { SourceName = "kafka-test-source", GroupId = "test-group" };

    private const string RegisteredSource = "aggregator-b";

    private static KafkaInboundRecord Record(
        string? value = ValidMessage, string? idempotencyKey = null, long offset = 42, string? source = null) =>
        new("bank-transactions", Partition: 1, Offset: offset, Key: "ext-acc-1", value, idempotencyKey,
            new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc), source);

    private sealed record Sut(
        BankTransactionsKafkaMessageHandler Handler, ISender Sender, IAuditTrail AuditTrail, IWebhookSourceDirectory Directory);

    private static Sut Build(Result<InboxReceipt>? result = null, KafkaOptions? options = null)
    {
        var sender = Substitute.For<ISender>();
        sender.Send(Arg.Any<ReceiveBankTransactionsCommand>(), Arg.Any<CancellationToken>())
            .Returns(result ?? Result.Success(new InboxReceipt(Guid.NewGuid(), IsDuplicate: false)));
        var auditTrail = Substitute.For<IAuditTrail>();
        var directory = Substitute.For<IWebhookSourceDirectory>();
        directory.IsActiveAsync(RegisteredSource, Arg.Any<CancellationToken>()).Returns(true);
        var handler = new BankTransactionsKafkaMessageHandler(
            sender, auditTrail, directory, Microsoft.Extensions.Options.Options.Create(options ?? Options));
        return new(handler, sender, auditTrail, directory);
    }

    [Fact]
    public async Task HandleAsync_ValidMessage_SendsTheSameCommandTheWebhookUses()
    {
        var sut = Build();

        var result = await sut.Handler.HandleAsync(Record(idempotencyKey: "delivery-1"), CancellationToken.None);

        result.Outcome.Should().Be(KafkaMessageOutcome.Enqueued);
        await sut.Sender.Received(1).Send(
            Arg.Is<ReceiveBankTransactionsCommand>(c =>
                c.SourceName == "kafka-test-source"
                && c.ExternalAccountId == "ext-acc-1"
                && c.IdempotencyKey == "delivery-1"
                && c.Transactions.Count == 1
                && c.Transactions[0].Id == "txn-1"
                && c.Transactions[0].Amount == -150m
                && c.Transactions[0].Category == string.Empty),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_ValidMessage_DescribesTheKafkaDeliveryForTheAuditTrail()
    {
        var sut = Build();

        await sut.Handler.HandleAsync(Record(offset: 1234), CancellationToken.None);

        await sut.Sender.Received(1).Send(
            Arg.Is<ReceiveBankTransactionsCommand>(c =>
                c.Delivery!.Channel == AuditChannels.Kafka
                && c.Delivery.Metadata["topic"] == "bank-transactions"
                && c.Delivery.Metadata["partition"] == "1"
                && c.Delivery.Metadata["offset"] == "1234"
                && c.Delivery.Metadata["key"] == "ext-acc-1"
                && c.Delivery.Metadata["consumerGroup"] == "test-group"
                && c.Delivery.Metadata.ContainsKey("producedAt")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_NoIdempotencyHeader_LeavesKeyForThePayloadHashFallback()
    {
        var sut = Build();

        await sut.Handler.HandleAsync(Record(idempotencyKey: null), CancellationToken.None);

        await sut.Sender.Received(1).Send(
            Arg.Is<ReceiveBankTransactionsCommand>(c => c.IdempotencyKey == null), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_DuplicateDelivery_ReportsDuplicate()
    {
        var inboxId = Guid.NewGuid();
        var sut = Build(Result.Success(new InboxReceipt(inboxId, IsDuplicate: true)));

        var result = await sut.Handler.HandleAsync(Record(), CancellationToken.None);

        result.Outcome.Should().Be(KafkaMessageOutcome.Duplicate);
        result.InboxMessageId.Should().Be(inboxId);
    }

    [Fact]
    public async Task HandleAsync_DuplicateOfDeadLetteredDelivery_ReportsEnqueuedBecauseItWasRequeued()
    {
        var sut = Build(Result.Success(new InboxReceipt(Guid.NewGuid(), IsDuplicate: true, Requeued: true)));

        var result = await sut.Handler.HandleAsync(Record(), CancellationToken.None);

        result.Outcome.Should().Be(KafkaMessageOutcome.Enqueued);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("""{ "externalAccountId": "ext-acc-1" }""")]
    public async Task HandleAsync_UnusablePayload_IsRejectedWithoutSendingAnything(string value)
    {
        var sut = Build();

        var result = await sut.Handler.HandleAsync(Record(value), CancellationToken.None);

        result.Outcome.Should().Be(KafkaMessageOutcome.Rejected);
        result.Reason.Should().NotBeNullOrEmpty();
        await sut.Sender.DidNotReceiveWithAnyArgs().Send(default(ReceiveBankTransactionsCommand)!, default);
    }

    [Fact]
    public async Task HandleAsync_ValidationFailure_IsRejected()
    {
        var sut = Build(Result.Failure<InboxReceipt>(Error.Validation("Currency must be 3 characters")));

        var result = await sut.Handler.HandleAsync(Record(), CancellationToken.None);

        result.Outcome.Should().Be(KafkaMessageOutcome.Rejected);
        result.Reason.Should().Contain("Currency");
    }

    [Fact]
    public async Task HandleAsync_Rejected_RecordsARejectionAuditEventWithKafkaCoordinates()
    {
        var sut = Build();

        await sut.Handler.HandleAsync(Record("not json", offset: 77), CancellationToken.None);

        await sut.AuditTrail.Received(1).RecordAsync(
            Arg.Is<IReadOnlyCollection<AuditEventRecord>>(events =>
                events.Count == 1
                && events.First().EventType == AuditEventTypes.InboundRejected
                && events.First().Channel == AuditChannels.Kafka
                && events.First().SourceName == "kafka-test-source"
                && events.First().Metadata!["offset"] == "77"
                && events.First().Metadata!["deadLetterTopic"] == Options.DeadLetterTopic
                && events.First().Detail!.StartsWith("Malformed JSON")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_SameRecordRejectedTwice_UsesTheSameAuditEventId()
    {
        var sut = Build();
        var ids = new List<Guid>();
        await sut.AuditTrail.RecordAsync(
            Arg.Do<IReadOnlyCollection<AuditEventRecord>>(events => ids.Add(events.Single().EventId)),
            Arg.Any<CancellationToken>());

        await sut.Handler.HandleAsync(Record("not json", offset: 5), CancellationToken.None);
        await sut.Handler.HandleAsync(Record("not json", offset: 5), CancellationToken.None);

        ids.Should().HaveCount(2);
        ids.Distinct().Should().ContainSingle("a retried record must map to the same audit fact, not a second one");
    }

    [Fact]
    public async Task HandleAsync_AuditWriteFails_ThrowsSoTheRecordIsRetriedNotDeadLetteredUnaudited()
    {
        var sut = Build();
        sut.AuditTrail.RecordAsync(Arg.Any<IReadOnlyCollection<AuditEventRecord>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("audit db down")));

        var act = () => sut.Handler.HandleAsync(Record("not json"), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task HandleAsync_NonValidationFailure_ThrowsSoTheConsumerRetriesInsteadOfDeadLettering()
    {
        var sut = Build(Result.Failure<InboxReceipt>(Error.Failure("Db.Unavailable", "database down")));

        var act = () => sut.Handler.HandleAsync(Record(), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task HandleAsync_NoSourceHeader_UsesTheConfiguredSourceWithoutARegistryLookup()
    {
        var sut = Build();

        await sut.Handler.HandleAsync(Record(source: null), CancellationToken.None);

        await sut.Sender.Received(1).Send(
            Arg.Is<ReceiveBankTransactionsCommand>(c => c.SourceName == "kafka-test-source"), Arg.Any<CancellationToken>());
        await sut.Directory.DidNotReceiveWithAnyArgs().IsActiveAsync(default!, default);
    }

    [Fact]
    public async Task HandleAsync_ActiveSourceHeader_IsUsedAsTheSourceName()
    {
        var sut = Build();

        var result = await sut.Handler.HandleAsync(Record(source: $"  {RegisteredSource} "), CancellationToken.None);

        result.Outcome.Should().Be(KafkaMessageOutcome.Enqueued);
        await sut.Sender.Received(1).Send(
            Arg.Is<ReceiveBankTransactionsCommand>(c =>
                c.SourceName == RegisteredSource
                && c.Delivery!.Metadata["sourceHeader"] == RegisteredSource),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_UnknownOrInactiveSourceHeader_IsRejectedUnderTheConfiguredSource()
    {
        var sut = Build();

        var result = await sut.Handler.HandleAsync(Record(source: "someone-else"), CancellationToken.None);

        result.Outcome.Should().Be(KafkaMessageOutcome.Rejected);
        result.Reason.Should().Contain("someone-else");
        await sut.Sender.DidNotReceiveWithAnyArgs().Send(default(ReceiveBankTransactionsCommand)!, default);
        await sut.AuditTrail.Received(1).RecordAsync(
            Arg.Is<IReadOnlyCollection<AuditEventRecord>>(events =>
                events.Single().SourceName == "kafka-test-source"
                && events.Single().Metadata!["sourceHeader"] == "someone-else"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_MissingSourceHeaderWhenRequired_IsRejected()
    {
        var sut = Build(options: new KafkaOptions { SourceName = "kafka-test-source", RequireSourceHeader = true });

        var result = await sut.Handler.HandleAsync(Record(source: " "), CancellationToken.None);

        result.Outcome.Should().Be(KafkaMessageOutcome.Rejected);
        result.Reason.Should().Contain(BankTransactionsKafkaMessageHandler.SourceHeader);
        await sut.Sender.DidNotReceiveWithAnyArgs().Send(default(ReceiveBankTransactionsCommand)!, default);
    }

    [Fact]
    public async Task HandleAsync_SourceLookupFails_ThrowsSoTheRecordIsRetriedNotDeadLettered()
    {
        var sut = Build();
        sut.Directory.IsActiveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<bool>(new InvalidOperationException("registry db down")));

        var act = () => sut.Handler.HandleAsync(Record(source: RegisteredSource), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        await sut.AuditTrail.DidNotReceiveWithAnyArgs().RecordAsync(default!, default);
    }

    [Fact]
    public async Task HandleAsync_IdempotencyKeyReusedForDifferentPayload_IsDeadLettered_NotRetriedForever()
    {
        var sut = Build(Result.Failure<InboxReceipt>(InboxErrors.IdempotencyKeyReused("delivery-1")));

        var result = await sut.Handler.HandleAsync(Record(idempotencyKey: "delivery-1"), CancellationToken.None);

        result.Outcome.Should().Be(KafkaMessageOutcome.Rejected,
            "a refused key reuse never succeeds on retry — throwing would stall the partition indefinitely");
    }

    [Fact]
    public async Task HandleAsync_MessageWithoutSchemaVersion_IsReadAsVersion1()
    {
        var sut = Build();

        await sut.Handler.HandleAsync(Record(), CancellationToken.None);

        await sut.Sender.Received(1).Send(
            Arg.Is<ReceiveBankTransactionsCommand>(c => c.SchemaVersion == BankTransactionsMessage.CurrentSchemaVersion),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_ExplicitSchemaVersion_IsPassedThroughForValidation()
    {
        var sut = Build();
        var v2 = ValidMessage.Replace("\"externalAccountId\"", "\"schemaVersion\": 2, \"externalAccountId\"");

        await sut.Handler.HandleAsync(Record(value: v2), CancellationToken.None);

        await sut.Sender.Received(1).Send(
            Arg.Is<ReceiveBankTransactionsCommand>(c => c.SchemaVersion == 2), Arg.Any<CancellationToken>());
    }
}