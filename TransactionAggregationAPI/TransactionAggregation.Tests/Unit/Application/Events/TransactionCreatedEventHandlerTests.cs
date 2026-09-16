using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Text.Json;
using TransactionAggregation.Application.Common.Interfaces;
using TransactionAggregation.Application.Common.Options;
using TransactionAggregation.Application.Common.Outbox;
using TransactionAggregation.Application.Features.Transactions.Events;
using TransactionAggregation.Application.Services;
using TransactionAggregation.Domain.Common.ValueObjects;
using TransactionAggregation.Domain.Entities;
using TransactionAggregation.Domain.Enums;
using TransactionAggregation.Domain.Events.Transaction;
using TransactionAggregation.Persistence;
using TransactionAggregation.Tests.Helpers;
using Xunit;

namespace TransactionAggregation.Tests.Unit.Application.Events;

public class TransactionCreatedEventHandlerTests
{
    private static readonly CategorizationOptions DefaultOptions = new()
    {
        Keywords = new Dictionary<string, string>
        {
            ["walmart"] = "Groceries",
            ["grocery"] = "Groceries",
            ["kroger"] = "Groceries",
            ["restaurant"] = "Dining",
            ["starbucks"] = "Dining",
            ["uber"] = "Transportation",
            ["lyft"] = "Transportation",
            ["netflix"] = "Entertainment",
            ["spotify"] = "Entertainment",
            ["electric"] = "Utilities",
            ["rent"] = "Housing",
            ["mortgage"] = "Housing",
        }
    };

    private static Transaction MakeTransaction(
        decimal amount, string description, TransactionCategory category = TransactionCategory.Uncategorized) =>
        Transaction.Create(
            CustomerId.Create(),
            Money.Create(amount, "ZAR"),
            description,
            category,
            TransactionSource.Create("test", Guid.NewGuid().ToString()));

    private static TransactionCreatedEventHandler BuildHandler(
        ApplicationDbContext context,
        ITransactionCategorizationService? categorization = null) =>
        new(
            NullLogger<TransactionCreatedEventHandler>.Instance,
            categorization ?? new TransactionCategorizationService(Options.Create(DefaultOptions)),
            context);

    [Theory]
    [InlineData("walmart weekly shop", TransactionCategory.Groceries)]
    [InlineData("kroger checkout", TransactionCategory.Groceries)]
    [InlineData("uber ride home", TransactionCategory.Transportation)]
    [InlineData("lyft to airport", TransactionCategory.Transportation)]
    [InlineData("netflix subscription", TransactionCategory.Entertainment)]
    [InlineData("spotify premium", TransactionCategory.Entertainment)]
    [InlineData("electric company", TransactionCategory.Utilities)]
    [InlineData("rent payment", TransactionCategory.Housing)]
    [InlineData("mortgage instalment", TransactionCategory.Housing)]
    [InlineData("restaurant dinner", TransactionCategory.Dining)]
    [InlineData("starbucks morning", TransactionCategory.Dining)]
    public async Task Handle_AutoCategorisesFromDescription(string description, TransactionCategory expected)
    {
        var context = InMemoryDbContextFactory.Create();
        var transaction = MakeTransaction(-100m, description);
        var handler = BuildHandler(context);

        await handler.Handle(new TransactionCreatedDomainEvent(transaction), CancellationToken.None);

        transaction.Category.Should().Be(expected);
    }

    [Fact]
    public async Task Handle_PositiveAmount_CategorisedAsIncome()
    {
        var context = InMemoryDbContextFactory.Create();
        var transaction = MakeTransaction(5000m, "salary payment");
        var handler = BuildHandler(context);

        await handler.Handle(new TransactionCreatedDomainEvent(transaction), CancellationToken.None);

        transaction.Category.Should().Be(TransactionCategory.Income);
    }

    [Fact]
    public async Task Handle_UnrecognisedNegativeDescription_RemainsUncategorized()
    {
        var context = InMemoryDbContextFactory.Create();
        var transaction = MakeTransaction(-75m, "payment xyz");
        var handler = BuildHandler(context);

        await handler.Handle(new TransactionCreatedDomainEvent(transaction), CancellationToken.None);

        transaction.Category.Should().Be(TransactionCategory.Uncategorized);
    }

    [Fact]
    public async Task Handle_AlreadyCategorizedTransaction_DoesNotOverwriteIt()
    {

        var context = InMemoryDbContextFactory.Create();
        var transaction = MakeTransaction(-100m, "uber ride home", TransactionCategory.Groceries);
        var handler = BuildHandler(context);

        await handler.Handle(new TransactionCreatedDomainEvent(transaction), CancellationToken.None);

        transaction.Category.Should().Be(TransactionCategory.Groceries);
    }

    [Fact]
    public async Task Handle_AlreadyCategorizedTransaction_DoesNotCallCategorizationService()
    {
        var context = InMemoryDbContextFactory.Create();
        var categorization = Substitute.For<ITransactionCategorizationService>();
        var transaction = MakeTransaction(-100m, "uber ride home", TransactionCategory.Groceries);
        var handler = BuildHandler(context, categorization);

        await handler.Handle(new TransactionCreatedDomainEvent(transaction), CancellationToken.None);

        await categorization.DidNotReceive().CategorizeTransactionAsync(Arg.Any<Transaction>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_AlreadyCategorizedTransaction_StillEnqueuesOutboxMessage()
    {
        var context = InMemoryDbContextFactory.Create();
        var transaction = MakeTransaction(-100m, "uber ride home", TransactionCategory.Groceries);
        var handler = BuildHandler(context);

        await handler.Handle(new TransactionCreatedDomainEvent(transaction), CancellationToken.None);
        await context.SaveChangesAsync();

        context.OutboxMessages.Should().ContainSingle(m => m.Type == OutboxMessageTypes.TransactionCreated);
    }

    [Fact]
    public async Task Handle_EnqueuesTransactionCreatedOutboxMessageWithCorrectPayload()
    {
        var context = InMemoryDbContextFactory.Create();
        var transaction = MakeTransaction(-100m, "test payment");
        var handler = BuildHandler(context);

        await handler.Handle(new TransactionCreatedDomainEvent(transaction), CancellationToken.None);
        await context.SaveChangesAsync();

        var message = context.OutboxMessages.Should().ContainSingle().Subject;
        message.Type.Should().Be(OutboxMessageTypes.TransactionCreated);

        var payload = JsonSerializer.Deserialize<TransactionCreatedOutboxPayload>(message.Payload)!;
        payload.TransactionId.Should().Be(transaction.Id.Value);
        payload.CustomerId.Should().Be(transaction.CustomerId.Value);
    }
}