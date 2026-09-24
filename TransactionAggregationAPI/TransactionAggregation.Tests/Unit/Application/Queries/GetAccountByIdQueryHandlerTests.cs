using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Modules.Customers.Application.Features.GetAccountById;
using Modules.Customers.Domain;
using Modules.Customers.Domain.ValueObjects;
using Modules.Customers.Infrastructure.Persistence;
using Modules.Transactions.Application.Adapters;
using Modules.Transactions.Domain.Common.ValueObjects;
using Modules.Transactions.Domain.Entities;
using Modules.Transactions.Domain.Enums;
using Modules.Transactions.Infrastructure.Persistence;
using SharedKernel.Common.Enums;
using SharedKernel.Common.ValueObjects;
using TransactionAggregation.Tests.Helpers;
using Xunit;

namespace TransactionAggregation.Tests.Unit.Application.Queries;

public class GetAccountByIdQueryHandlerTests
{
    // Balances come from the Transactions module via IAccountBalanceProvider; the real
    // TransactionBalanceProvider is used so the summing logic is exercised end to end.
    private static GetAccountByIdQueryHandler BuildHandler(
        CustomersDbContext ctx,
        TransactionsDbContext? transactionsCtx = null)
        => new(ctx,
            new TransactionBalanceProvider(transactionsCtx ?? InMemoryDbContextFactory.Create()),
            NullLogger<GetAccountByIdQueryHandler>.Instance);

    private static async Task<Account> SeedAccountAsync(
        CustomersDbContext ctx,
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
        var context = InMemoryCustomersDbContextFactory.Create();
        var account = await SeedAccountAsync(context, "ACC-123", "My Savings", AccountType.Savings, "USD");
        var handler = BuildHandler(context);

        var result = await handler.Handle(
            new GetAccountByIdQuery(account.Id.Value, account.CustomerId.Value), CancellationToken.None);

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
        var context = InMemoryCustomersDbContextFactory.Create();
        var customerId = CustomerId.Create();
        var account = Account.Create(customerId, "ACC-001", "My Account", AccountType.Checking);
        context.Accounts.Add(account);
        await context.SaveChangesAsync();

        var handler = BuildHandler(context);
        var result = await handler.Handle(
            new GetAccountByIdQuery(account.Id.Value, account.CustomerId.Value), CancellationToken.None);

        result.Value.CustomerId.Should().Be(customerId.Value);
    }

    [Fact]
    public async Task Handle_DeactivatedAccount_ReturnsIsActiveFalse()
    {
        var context = InMemoryCustomersDbContextFactory.Create();
        var account = await SeedAccountAsync(context);
        account.Deactivate();
        await context.SaveChangesAsync();

        var handler = BuildHandler(context);
        var result = await handler.Handle(
            new GetAccountByIdQuery(account.Id.Value, account.CustomerId.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_NonExistentAccount_ReturnsNotFound()
    {
        var context = InMemoryCustomersDbContextFactory.Create();
        var handler = BuildHandler(context);

        var result = await handler.Handle(
            new GetAccountByIdQuery(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Handle_WithMultipleAccounts_ReturnsCorrectOne()
    {
        var context = InMemoryCustomersDbContextFactory.Create();
        var acc1 = await SeedAccountAsync(context, "ACC-001", "First");
        var acc2 = await SeedAccountAsync(context, "ACC-002", "Second");

        var handler = BuildHandler(context);
        var result = await handler.Handle(
            new GetAccountByIdQuery(acc2.Id.Value, acc2.CustomerId.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.AccountNumber.Should().Be("ACC-002");
        result.Value.AccountName.Should().Be("Second");
    }

    [Fact]
    public async Task Handle_ExistingAccount_MapsCreatedAtCorrectly()
    {
        var context = InMemoryCustomersDbContextFactory.Create();
        var account = await SeedAccountAsync(context);
        var handler = BuildHandler(context);

        var result = await handler.Handle(
            new GetAccountByIdQuery(account.Id.Value, account.CustomerId.Value), CancellationToken.None);

        result.Value.CreatedAt.Should().NotBe(default);
    }

    /// <summary>
    /// The balance is computed from the account's transactions; these pin non-zero cases, so a
    /// fallback to zero can't pass unnoticed.
    /// </summary>
    [Fact]
    public async Task Handle_AccountWithLinkedTransactions_ReturnsSummedBalance()
    {
        var context = InMemoryCustomersDbContextFactory.Create();
        var transactionsCtx = InMemoryDbContextFactory.Create();
        var account = await SeedAccountAsync(context);

        transactionsCtx.Transactions.Add(Transaction.Create(
            account.CustomerId, Money.Create(1000m, "ZAR"), "Salary",
            TransactionCategory.Income, TransactionSource.Create("Bank A", "ext-1"), account.Id).Settled());
        transactionsCtx.Transactions.Add(Transaction.Create(
            account.CustomerId, Money.Create(-150m, "ZAR"), "Groceries",
            TransactionCategory.Groceries, TransactionSource.Create("Bank A", "ext-2"), account.Id).Settled());
        await transactionsCtx.SaveChangesAsync();

        var handler = BuildHandler(context, transactionsCtx);
        var result = await handler.Handle(
            new GetAccountByIdQuery(account.Id.Value, account.CustomerId.Value), CancellationToken.None);

        result.Value.Balance.Should().Be(850m);
    }

    [Fact]
    public async Task Handle_TransactionLinkedToAnotherAccount_IsExcludedFromBalance()
    {
        var context = InMemoryCustomersDbContextFactory.Create();
        var account = await SeedAccountAsync(context, "ACC-001");
        var otherAccount = await SeedAccountAsync(context, "ACC-002");

        var transactionsCtx = InMemoryDbContextFactory.Create();
        transactionsCtx.Transactions.Add(Transaction.Create(
            account.CustomerId, Money.Create(500m, "ZAR"), "This account's income",
            TransactionCategory.Income, TransactionSource.Create("Bank A", "ext-1"), account.Id).Settled());
        transactionsCtx.Transactions.Add(Transaction.Create(
            account.CustomerId, Money.Create(999m, "ZAR"), "Other account's income",
            TransactionCategory.Income, TransactionSource.Create("Bank A", "ext-2"), otherAccount.Id).Settled());
        await transactionsCtx.SaveChangesAsync();

        var handler = BuildHandler(context, transactionsCtx);
        var result = await handler.Handle(
            new GetAccountByIdQuery(account.Id.Value, account.CustomerId.Value), CancellationToken.None);

        result.Value.Balance.Should().Be(500m);
    }

    [Fact]
    public async Task Handle_AnotherCustomersAccount_ReturnsNotFoundButOwnerCanReadIt()
    {
        var context = InMemoryCustomersDbContextFactory.Create();
        var account = await SeedAccountAsync(context);
        var handler = BuildHandler(context);

        var notOwned = await handler.Handle(
            new GetAccountByIdQuery(account.Id.Value, Guid.NewGuid()), CancellationToken.None);
        var asOwner = await handler.Handle(
            new GetAccountByIdQuery(account.Id.Value, account.CustomerId.Value), CancellationToken.None);

        notOwned.IsFailure.Should().BeTrue();
        notOwned.Error.Type.Should().Be(ErrorType.NotFound);
        asOwner.IsSuccess.Should().BeTrue("the owner can still read it");
        notOwned.Error.Should().Be(Modules.Customers.Application.Errors.AccountErrors.NotFound(account.Id.Value));
    }
}