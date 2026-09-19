using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using SharedKernel.Common.Enums;
using TransactionAggregation.Application.Queries.Account.GetCustomerAccounts;
using TransactionAggregation.Domain.Common.ValueObjects;
using TransactionAggregation.Domain.Entities;
using TransactionAggregation.Domain.Enums;
using TransactionAggregation.Tests.Helpers;
using Xunit;

namespace TransactionAggregation.Tests.Unit.Application.Queries;

public class GetCustomerAccountsQueryHandlerTests
{
    private static GetCustomerAccountsQueryHandler BuildHandler(
        TransactionAggregation.Persistence.ApplicationDbContext ctx)
        => new(ctx, NullLogger<GetCustomerAccountsQueryHandler>.Instance);

    [Fact]
    public async Task Handle_NonExistentCustomer_ReturnsNotFound()
    {
        var context = InMemoryDbContextFactory.Create();
        var handler = BuildHandler(context);

        var result = await handler.Handle(
            new GetCustomerAccountsQuery(Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Handle_CustomerWithNoAccounts_ReturnsEmpty()
    {
        var context = InMemoryDbContextFactory.Create();
        var customer = Customer.Create(CustomerId.Create(), "a@b.com", "Test");
        context.Customers.Add(customer);
        await context.SaveChangesAsync();

        var handler = BuildHandler(context);
        var result = await handler.Handle(
            new GetCustomerAccountsQuery(customer.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    /// <summary>
    /// Account.Balance is never maintained by any handler (Credit()/Debit() are
    /// domain-tested but unwired) — reading it directly is always 0. This handler
    /// computes each account's balance from its own linked transactions; the
    /// critical case (missed in a naive fix) is that transactions must be summed
    /// per-account, not pooled across every account the customer owns.
    /// </summary>
    [Fact]
    public async Task Handle_MultipleAccountsWithTransactions_SumsBalancePerAccountIndependently()
    {
        var context = InMemoryDbContextFactory.Create();
        var customer = Customer.Create(CustomerId.Create(), "a@b.com", "Test");
        context.Customers.Add(customer);

        var checking = Account.Create(customer.Id, "ACC-001", "Checking", AccountType.Checking);
        var savings = Account.Create(customer.Id, "ACC-002", "Savings", AccountType.Savings);
        context.Accounts.AddRange(checking, savings);

        context.Transactions.Add(Transaction.Create(
            customer.Id, Money.Create(1000m, "ZAR"), "Salary",
            TransactionCategory.Income, TransactionSource.Create("Bank A", "ext-1"), checking.Id));
        context.Transactions.Add(Transaction.Create(
            customer.Id, Money.Create(-200m, "ZAR"), "Rent",
            TransactionCategory.Housing, TransactionSource.Create("Bank A", "ext-2"), checking.Id));
        context.Transactions.Add(Transaction.Create(
            customer.Id, Money.Create(5000m, "ZAR"), "Deposit",
            TransactionCategory.Income, TransactionSource.Create("Bank A", "ext-3"), savings.Id));
        await context.SaveChangesAsync();

        var handler = BuildHandler(context);
        var result = await handler.Handle(
            new GetCustomerAccountsQuery(customer.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value.Single(a => a.Id == checking.Id.Value).Balance.Should().Be(800m);
        result.Value.Single(a => a.Id == savings.Id.Value).Balance.Should().Be(5000m);
    }

    [Fact]
    public async Task Handle_AccountWithNoTransactions_ReturnsZeroBalance()
    {
        var context = InMemoryDbContextFactory.Create();
        var customer = Customer.Create(CustomerId.Create(), "a@b.com", "Test");
        var account = Account.Create(customer.Id, "ACC-001", "Checking", AccountType.Checking);
        context.Customers.Add(customer);
        context.Accounts.Add(account);
        await context.SaveChangesAsync();

        var handler = BuildHandler(context);
        var result = await handler.Handle(
            new GetCustomerAccountsQuery(customer.Id.Value), CancellationToken.None);

        result.Value.Single().Balance.Should().Be(0m);
    }

    /// <summary>
    /// A transaction with no AccountId (not yet linked/categorized to a specific
    /// account) must not blow up the grouping query or silently attach to the
    /// wrong account.
    /// </summary>
    [Fact]
    public async Task Handle_UnlinkedTransaction_DoesNotAffectAnyAccountBalance()
    {
        var context = InMemoryDbContextFactory.Create();
        var customer = Customer.Create(CustomerId.Create(), "a@b.com", "Test");
        var account = Account.Create(customer.Id, "ACC-001", "Checking", AccountType.Checking);
        context.Customers.Add(customer);
        context.Accounts.Add(account);

        context.Transactions.Add(Transaction.Create(
            customer.Id, Money.Create(300m, "ZAR"), "Unlinked",
            TransactionCategory.Uncategorized, TransactionSource.Create("Bank A", "ext-1"), accountId: null));
        await context.SaveChangesAsync();

        var handler = BuildHandler(context);
        var result = await handler.Handle(
            new GetCustomerAccountsQuery(customer.Id.Value), CancellationToken.None);

        result.Value.Single().Balance.Should().Be(0m);
    }
}
