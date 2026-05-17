using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using TransactionAggregation.Application.Common.Interfaces;
using TransactionAggregation.Application.Common.Options;
using TransactionAggregation.Application.Features.Transactions.Events;
using TransactionAggregation.Application.Services;
using TransactionAggregation.Domain.Common.ValueObjects;
using TransactionAggregation.Domain.Entities;
using TransactionAggregation.Domain.Enums;
using TransactionAggregation.Domain.Events.Transaction;
using Xunit;

namespace TransactionAggregation.Tests.Unit.Application.Events;

public class TransactionCreatedEventHandlerTests
{
    private static readonly CategorizationOptions DefaultOptions = new()
    {
        Keywords = new Dictionary<string, string>
        {
            ["walmart"]    = "Groceries",
            ["grocery"]    = "Groceries",
            ["kroger"]     = "Groceries",
            ["restaurant"] = "Dining",
            ["starbucks"]  = "Dining",
            ["uber"]       = "Transportation",
            ["lyft"]       = "Transportation",
            ["netflix"]    = "Entertainment",
            ["spotify"]    = "Entertainment",
            ["electric"]   = "Utilities",
            ["rent"]       = "Housing",
            ["mortgage"]   = "Housing",
        }
    };

    private static Transaction MakeTransaction(decimal amount, string description) =>
        Transaction.Create(
            CustomerId.Create(),
            Money.Create(amount, "ZAR"),
            description,
            TransactionCategory.Uncategorized,
            TransactionSource.Create("test", Guid.NewGuid().ToString()));

    private static TransactionCreatedEventHandler BuildHandler(
        IAnalyticsService? analytics = null,
        INotificationService? notifications = null,
        ITransactionCategorizationService? categorization = null) =>
        new(
            NullLogger<TransactionCreatedEventHandler>.Instance,
            categorization ?? new TransactionCategorizationService(Options.Create(DefaultOptions)),
            analytics ?? Substitute.For<IAnalyticsService>(),
            notifications ?? Substitute.For<INotificationService>());

    // ── Auto-categorisation ───────────────────────────────────────────────────

    [Theory]
    [InlineData("walmart weekly shop",    TransactionCategory.Groceries)]
    [InlineData("kroger checkout",        TransactionCategory.Groceries)]
    [InlineData("uber ride home",         TransactionCategory.Transportation)]
    [InlineData("lyft to airport",        TransactionCategory.Transportation)]
    [InlineData("netflix subscription",   TransactionCategory.Entertainment)]
    [InlineData("spotify premium",        TransactionCategory.Entertainment)]
    [InlineData("electric company",       TransactionCategory.Utilities)]
    [InlineData("rent payment",           TransactionCategory.Housing)]
    [InlineData("mortgage instalment",    TransactionCategory.Housing)]
    [InlineData("restaurant dinner",      TransactionCategory.Dining)]
    [InlineData("starbucks morning",      TransactionCategory.Dining)]
    public async Task Handle_AutoCategorisesFromDescription(string description, TransactionCategory expected)
    {
        var transaction = MakeTransaction(-100m, description);
        var handler = BuildHandler();

        await handler.Handle(new TransactionCreatedDomainEvent(transaction), CancellationToken.None);

        transaction.Category.Should().Be(expected);
    }

    [Fact]
    public async Task Handle_PositiveAmount_CategorisedAsIncome()
    {
        var transaction = MakeTransaction(5000m, "salary payment");
        var handler = BuildHandler();

        await handler.Handle(new TransactionCreatedDomainEvent(transaction), CancellationToken.None);

        transaction.Category.Should().Be(TransactionCategory.Income);
    }

    [Fact]
    public async Task Handle_UnrecognisedNegativeDescription_RemainsUncategorized()
    {
        var transaction = MakeTransaction(-75m, "payment xyz");
        var handler = BuildHandler();

        await handler.Handle(new TransactionCreatedDomainEvent(transaction), CancellationToken.None);

        transaction.Category.Should().Be(TransactionCategory.Uncategorized);
    }

    // ── Side-effects ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_TracksCreationAnalytics()
    {
        var analytics = Substitute.For<IAnalyticsService>();
        var transaction = MakeTransaction(-100m, "test payment");
        var handler = BuildHandler(analytics: analytics);

        await handler.Handle(new TransactionCreatedDomainEvent(transaction), CancellationToken.None);

        await analytics.Received(1).TrackTransactionCreatedAsync(transaction, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_SendsCreationNotification()
    {
        var notifications = Substitute.For<INotificationService>();
        var transaction = MakeTransaction(-100m, "test payment");
        var handler = BuildHandler(notifications: notifications);

        await handler.Handle(new TransactionCreatedDomainEvent(transaction), CancellationToken.None);

        await notifications.Received(1).SendTransactionNotificationAsync(
            transaction,
            NotificationType.TransactionCreated,
            Arg.Any<CancellationToken>());
    }
}
