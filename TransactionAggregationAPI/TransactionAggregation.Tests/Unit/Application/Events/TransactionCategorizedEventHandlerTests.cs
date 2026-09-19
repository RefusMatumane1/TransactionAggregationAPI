using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;
using BuildingBlocks.Messaging.Persistence;
using TransactionAggregation.Application.Common.Outbox;
using TransactionAggregation.Application.Features.Transactions.Events;
using TransactionAggregation.Domain.Common.ValueObjects;
using TransactionAggregation.Domain.Entities;
using TransactionAggregation.Domain.Enums;
using TransactionAggregation.Domain.Events.Transaction;
using TransactionAggregation.Tests.Helpers;
using Xunit;

namespace TransactionAggregation.Tests.Unit.Application.Events;

public class TransactionCategorizedEventHandlerTests
{
    private static Transaction MakeTransaction() =>
        Transaction.Create(
            CustomerId.Create(),
            Money.Create(-100m, "ZAR"),
            "test payment",
            TransactionCategory.Uncategorized,
            TransactionSource.Create("test", Guid.NewGuid().ToString()));

    private static TransactionCategorizedEventHandler BuildHandler(IMessagingDbContext messaging) =>
        new(NullLogger<TransactionCategorizedEventHandler>.Instance, messaging);

    [Fact]
    public async Task Handle_EnqueuesTransactionCategorizedOutboxMessageWithCorrectPayload()
    {
        var messaging = InMemoryMessagingDbContextFactory.Create();
        var transaction = MakeTransaction();
        var handler = BuildHandler(messaging);
        var domainEvent = new TransactionCategorizedDomainEvent(
            transaction, TransactionCategory.Uncategorized, TransactionCategory.Groceries, isAutoCategorized: true);

        await handler.Handle(domainEvent, CancellationToken.None);
        await messaging.SaveChangesAsync();

        var message = messaging.OutboxMessages.Should().ContainSingle().Subject;
        message.Type.Should().Be(OutboxMessageTypes.TransactionCategorized);

        var payload = JsonSerializer.Deserialize<TransactionCategorizedOutboxPayload>(message.Payload)!;
        payload.TransactionId.Should().Be(transaction.Id.Value);
        payload.CustomerId.Should().Be(transaction.CustomerId.Value);
        payload.OldCategory.Should().Be(TransactionCategory.Uncategorized);
        payload.NewCategory.Should().Be(TransactionCategory.Groceries);
        payload.IsAutoCategorized.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ManualRecategorization_EnqueuesPayloadWithIsAutoCategorizedFalse()
    {
        var messaging = InMemoryMessagingDbContextFactory.Create();
        var transaction = MakeTransaction();
        var handler = BuildHandler(messaging);
        var domainEvent = new TransactionCategorizedDomainEvent(
            transaction, TransactionCategory.Groceries, TransactionCategory.Dining, isAutoCategorized: false);

        await handler.Handle(domainEvent, CancellationToken.None);
        await messaging.SaveChangesAsync();

        var message = messaging.OutboxMessages.Should().ContainSingle().Subject;
        var payload = JsonSerializer.Deserialize<TransactionCategorizedOutboxPayload>(message.Payload)!;
        payload.IsAutoCategorized.Should().BeFalse();
    }
}
