using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using SharedKernel.Common.Enums;
using TransactionAggregation.Application.Queries.Account.GetAccountById;
using TransactionAggregation.Domain.Common.ValueObjects;
using TransactionAggregation.Domain.Entities;
using TransactionAggregation.Domain.Enums;
using TransactionAggregation.Tests.Helpers;
using Xunit;

namespace TransactionAggregation.Tests.Unit.Application.Queries;

public class GetAccountByIdQueryHandlerTests
{
    private static GetAccountByIdQueryHandler BuildHandler(
        TransactionAggregation.Persistence.ApplicationDbContext ctx)
        => new(ctx, NullLogger<GetAccountByIdQueryHandler>.Instance);

    private static async Task<Account> SeedAccountAsync(
        TransactionAggregation.Persistence.ApplicationDbContext ctx,
        string accountNumber = "ACC-001",
        string accountName = "Test Account",
        AccountType type = AccountType.Checking,
        string currency = "ZAR")
    {
        var account = Account.Create(CustomerId.Create(), accountNumber, accountName, type, currency);
        ctx.Accounts.Add(account);
        await ctx.SaveChangesAsync();
        return account;
    }

    [Fact]
    public async Task Handle_ExistingAccount_ReturnsMappedDto()
    {
        var context = InMemoryDbContextFactory.Create();
        var account = await SeedAccountAsync(context, "ACC-123", "My Savings", AccountType.Savings, "USD");
        var handler = BuildHandler(context);

        var result = await handler.Handle(
            new GetAccountByIdQuery(account.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(account.Id.Value);
        result.Value.AccountNumber.Should().Be("ACC-123");
        result.Value.AccountName.Should().Be("My Savings");
        result.Value.AccountType.Should().Be(AccountType.Savings);
        result.Value.Currency.Should().Be("USD");
        result.Value.Balance.Should().Be(0m);
        result.Value.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ExistingAccount_MapsCustomerIdCorrectly()
    {
        var context = InMemoryDbContextFactory.Create();
        var customerId = CustomerId.Create();
        var account = Account.Create(customerId, "ACC-001", "My Account", AccountType.Checking);
        context.Accounts.Add(account);
        await context.SaveChangesAsync();

        var handler = BuildHandler(context);
        var result = await handler.Handle(
            new GetAccountByIdQuery(account.Id.Value), CancellationToken.None);

        result.Value.CustomerId.Should().Be(customerId.Value);
    }

    [Fact]
    public async Task Handle_DeactivatedAccount_ReturnsIsActiveFalse()
    {
        var context = InMemoryDbContextFactory.Create();
        var account = await SeedAccountAsync(context);
        account.Deactivate();
        await context.SaveChangesAsync();

        var handler = BuildHandler(context);
        var result = await handler.Handle(
            new GetAccountByIdQuery(account.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_NonExistentAccount_ReturnsNotFound()
    {
        var context = InMemoryDbContextFactory.Create();
        var handler = BuildHandler(context);

        var result = await handler.Handle(
            new GetAccountByIdQuery(Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Handle_WithMultipleAccounts_ReturnsCorrectOne()
    {
        var context = InMemoryDbContextFactory.Create();
        var acc1 = await SeedAccountAsync(context, "ACC-001", "First");
        var acc2 = await SeedAccountAsync(context, "ACC-002", "Second");

        var handler = BuildHandler(context);
        var result = await handler.Handle(
            new GetAccountByIdQuery(acc2.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.AccountNumber.Should().Be("ACC-002");
        result.Value.AccountName.Should().Be("Second");
    }

    [Fact]
    public async Task Handle_ExistingAccount_MapsCreatedAtCorrectly()
    {
        var context = InMemoryDbContextFactory.Create();
        var account = await SeedAccountAsync(context);
        var handler = BuildHandler(context);

        var result = await handler.Handle(
            new GetAccountByIdQuery(account.Id.Value), CancellationToken.None);

        result.Value.CreatedAt.Should().NotBe(default);
    }

    /// <summary>
    /// Account.Balance is never maintained by any handler (Credit()/Debit() are
    /// domain-tested but unwired) — reading it directly is always 0. The handler
    /// computes Balance from linked transactions instead; these pin that it
    /// actually reflects reality, not just that it defaults to 0 like the tests
    /// above (seeded with no transactions) would still pass even if the computed
    /// path silently fell back to the dead stored field.
    /// </summary>
    [Fact]
    public async Task Handle_AccountWithLinkedTransactions_ReturnsSummedBalance()
    {
        var context = InMemoryDbContextFactory.Create();
        var account = await SeedAccountAsync(context);

        context.Transactions.Add(Transaction.Create(
            account.CustomerId, Money.Create(1000m, "ZAR"), "Salary",
            TransactionCategory.Income, TransactionSource.Create("Bank A", "ext-1"), account.Id));
        context.Transactions.Add(Transaction.Create(
            account.CustomerId, Money.Create(-150m, "ZAR"), "Groceries",
            TransactionCategory.Groceries, TransactionSource.Create("Bank A", "ext-2"), account.Id));
        await context.SaveChangesAsync();

        var handler = BuildHandler(context);
        var result = await handler.Handle(
            new GetAccountByIdQuery(account.Id.Value), CancellationToken.None);

        result.Value.Balance.Should().Be(850m);
    }

    [Fact]
    public async Task Handle_TransactionLinkedToAnotherAccount_IsExcludedFromBalance()
    {
        var context = InMemoryDbContextFactory.Create();
        var account = await SeedAccountAsync(context, "ACC-001");
        var otherAccount = await SeedAccountAsync(context, "ACC-002");

        context.Transactions.Add(Transaction.Create(
            account.CustomerId, Money.Create(500m, "ZAR"), "This account's income",
            TransactionCategory.Income, TransactionSource.Create("Bank A", "ext-1"), account.Id));
        context.Transactions.Add(Transaction.Create(
            account.CustomerId, Money.Create(999m, "ZAR"), "Other account's income",
            TransactionCategory.Income, TransactionSource.Create("Bank A", "ext-2"), otherAccount.Id));
        await context.SaveChangesAsync();

        var handler = BuildHandler(context);
        var result = await handler.Handle(
            new GetAccountByIdQuery(account.Id.Value), CancellationToken.None);

        result.Value.Balance.Should().Be(500m);
    }
}