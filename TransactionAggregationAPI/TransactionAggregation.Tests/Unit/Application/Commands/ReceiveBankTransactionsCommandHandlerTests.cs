using FluentAssertions;
using System.Text.Json;
using TransactionAggregation.Application.Common.DTOs;
using TransactionAggregation.Application.Common.Inbox;
using TransactionAggregation.Application.Features.Transactions.Commands.ReceiveBankTransactions;
using TransactionAggregation.Domain.Inbox;
using TransactionAggregation.Persistence;
using TransactionAggregation.Tests.Helpers;
using Xunit;

namespace TransactionAggregation.Tests.Unit.Application.Commands;

public class ReceiveBankTransactionsCommandHandlerTests
{
    private static ReceiveBankTransactionsCommandHandler BuildHandler(ApplicationDbContext ctx) => new(ctx);

    private static ExternalTransactionDTO MakeDto(string id = "txn-1", decimal amount = -150m) => new()
    {
        Id = id,
        Amount = amount,
        Currency = "ZAR",
        Description = "Woolworths",
        Category = string.Empty,
        Date = DateTime.UtcNow
    };

    [Fact]
    public async Task Handle_ValidRequest_CreatesAPendingInboxMessage()
    {
        var context = InMemoryDbContextFactory.Create();
        var handler = BuildHandler(context);

        var result = await handler.Handle(
            new ReceiveBankTransactionsCommand("test-source", "ext-acc-1", [MakeDto()]), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var message = context.InboxMessages.Should().ContainSingle().Subject;
        message.Id.Value.Should().Be(result.Value);
        message.SourceName.Should().Be("test-source");
        message.Status.Should().Be(InboxMessageStatus.Pending);
    }

    [Fact]
    public async Task Handle_ValidRequest_PayloadRoundTripsExternalAccountIdAndTransactions()
    {
        var context = InMemoryDbContextFactory.Create();
        var handler = BuildHandler(context);
        var dto = MakeDto("txn-42", amount: -75.50m);

        await handler.Handle(
            new ReceiveBankTransactionsCommand("test-source", "ext-acc-7", [dto]), CancellationToken.None);

        var message = context.InboxMessages.Single();
        var payload = JsonSerializer.Deserialize<InboundTransactionsPayload>(message.Payload)!;

        payload.ExternalAccountId.Should().Be("ext-acc-7");
        payload.Transactions.Should().ContainSingle();
        payload.Transactions[0].Id.Should().Be("txn-42");
        payload.Transactions[0].Amount.Should().Be(-75.50m);
    }

    [Fact]
    public async Task Handle_DoesNotResolveBankLinkOrPersistTransactions()
    {
        // Business validation (does this BankLink even exist?) is deliberately deferred to
        // ProcessInboundTransactionsCommand, run later by InboxDispatcherBackgroundService — this
        // handler only writes the raw payload, regardless of whether "never-linked" is real.
        var context = InMemoryDbContextFactory.Create();
        var handler = BuildHandler(context);

        var result = await handler.Handle(
            new ReceiveBankTransactionsCommand("test-source", "never-linked", [MakeDto()]), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        context.Transactions.Should().BeEmpty();
        context.InboxMessages.Should().ContainSingle();
    }
}
