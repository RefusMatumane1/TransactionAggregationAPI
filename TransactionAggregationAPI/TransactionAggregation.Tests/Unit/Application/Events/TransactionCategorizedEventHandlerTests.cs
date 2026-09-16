using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;
using TransactionAggregation.Application.Common.Outbox;
using TransactionAggregation.Application.Features.Transactions.Events;
using TransactionAggregation.Domain.Common.ValueObjects;
using TransactionAggregation.Domain.Entities;
using TransactionAggregation.Domain.Enums;
using TransactionAggregation.Domain.Events.Transaction;
using TransactionAggregation.Persistence;
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

    private static TransactionCategorizedEventHandler BuildHandler(ApplicationDbContext context) =>
        new(NullLogger<TransactionCategorizedEventHandler>.Instance, context);

    [Fact]
    public async Task Handle_EnqueuesTransactionCategorizedOutboxMessageWithCorrectPayload()
    {
        var context = InMemoryDbContextFactory.Create();
        var transaction = MakeTransaction();
        var handler = BuildHandler(context);
        var domainEvent = new TransactionCategorizedDomainEvent(
            transaction, TransactionCategory.Uncategorized, TransactionCategory.Groceries, isAutoCategorized: true);

        await handler.Handle(domainEvent, CancellationToken.None);
        await context.SaveChangesAsync();

        var message = context.OutboxMessages.Should().ContainSingle().Subject;
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
        var context = InMemoryDbContextFactory.Create();
        var transaction = MakeTransaction();
        var handler = BuildHandler(context);
        var domainEvent = new TransactionCategorizedDomainEvent(
            transaction, TransactionCategory.Groceries, TransactionCategory.Dining, isAutoCategorized: false);

        await handler.Handle(domainEvent, CancellationToken.None);
        await context.SaveChangesAsync();

        var message = context.OutboxMessages.Should().ContainSingle().Subject;
        var payload = JsonSerializer.Deserialize<TransactionCategorizedOutboxPayload>(message.Payload)!;
        payload.IsAutoCategorized.Should().BeFalse();
    }
}
