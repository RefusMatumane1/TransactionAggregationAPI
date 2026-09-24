using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Modules.Customers.Application.Contracts;
using Modules.Customers.Domain;
using Modules.Customers.Domain.ValueObjects;
using Modules.Transactions.Application.Adapters;
using Modules.Transactions.Application.Features.Transactions.Queries.GetCustomerWithTransactions;
using Modules.Transactions.Application.Features.Transactions.Queries.GetTransactionSummary;
using Modules.Transactions.Domain.Common.ValueObjects;
using Modules.Transactions.Domain.Entities;
using Modules.Transactions.Domain.Enums;
using SharedKernel.Common.ValueObjects;
using TransactionAggregation.Tests.Helpers;
using Xunit;

namespace TransactionAggregation.Tests.Unit.Application.Queries;

/// <summary>
/// The customer+transactions endpoint, the summary and account balances all use TransactionTotals;
/// these pin that their figures agree.
/// </summary>
public class TotalsConsistencyTests
{
    private static readonly DateTime Day = new(2026, 9, 10, 12, 0, 0, DateTimeKind.Utc);

    private sealed record Scenario(
        Modules.Transactions.Infrastructure.Persistence.TransactionsDbContext Transactions,
        Modules.Customers.Infrastructure.Persistence.CustomersDbContext Customers,
        Customer Customer,
        Account Account);

    /// <summary>
    /// Booked: +1000 income, -300 and -200 expenses → net 500.
    /// Pending: +50 incoming, -80 outgoing. Rejected -999 and Cancelled +777 count nowhere.
    /// </summary>
    private static async Task<Scenario> SeedAsync()
    {
        var customers = InMemoryCustomersDbContextFactory.Create();
        var customer = Customer.Create(CustomerId.Create(), $"{Guid.NewGuid()}@example.com", "Totals Test");
        var account = Account.Create(customer.Id, "ACC-TOT", "Everyday", AccountType.Checking);
        customers.Customers.Add(customer);
        customers.Accounts.Add(account);
        await customers.SaveChangesAsync();

        Transaction Tx(decimal amount, TransactionStatus status, int n)
        {
            var tx = Transaction.Create(customer.Id, Money.Create(amount, "ZAR"), $"tx-{n}",
                amount > 0 ? TransactionCategory.Income : TransactionCategory.Groceries,
                TransactionSource.Create("Bank A", $"ext-{n}"), account.Id, Day.AddMinutes(n));
            if (status == TransactionStatus.Settled)
                tx.Settle();
            else if (status != TransactionStatus.Pending)
                tx.UpdateStatus(status);
            return tx;
        }

        var transactions = InMemoryDbContextFactory.Create();
        transactions.Transactions.AddRange(
            Tx(1000m, TransactionStatus.Settled, 1),
            Tx(-300m, TransactionStatus.Settled, 2),
            Tx(-200m, TransactionStatus.Settled, 3),
            Tx(50m, TransactionStatus.Pending, 4),
            Tx(-80m, TransactionStatus.Pending, 5),
            Tx(-999m, TransactionStatus.Rejected, 6),
            Tx(777m, TransactionStatus.Cancelled, 7));
        await transactions.SaveChangesAsync();

        return new Scenario(transactions, customers, customer, account);
    }

    [Fact]
    public async Task Summary_CountsBookedOnly_AndReportsPendingSeparately()
    {
        var s = await SeedAsync();
        var handler = new GetTransactionSummaryQueryHandler(s.Transactions, NullLogger<GetTransactionSummaryQueryHandler>.Instance);

        var summary = (await handler.Handle(
            new GetTransactionSummaryQuery(s.Customer.Id.Value, Day.AddDays(-1), Day.AddDays(1)), CancellationToken.None)).Value;

        summary.TotalIncome.Should().Be(1000m);
        summary.TotalExpenses.Should().Be(500m);
        summary.NetBalance.Should().Be(500m);
        summary.SpendingByCategory[TransactionCategory.Groceries].Should().Be(500m);
        summary.MonthlySummaries.Should().ContainSingle().Which.NetBalance.Should().Be(500m);
        summary.PendingIncome.Should().Be(50m);
        summary.PendingExpenses.Should().Be(80m);
        summary.TotalTransactions.Should().Be(7);
        summary.CompletedTransactions.Should().Be(3);
        summary.PendingTransactions.Should().Be(2);
    }

    [Fact]
    public async Task CustomerWithTransactions_TotalsCoverTheWholeRange_NotJustTheCurrentPage()
    {
        var s = await SeedAsync();
        var handler = new GetCustomerWithTransactionsQueryHandler(
            s.Transactions, new CustomersReadApi(s.Customers), NullLogger<GetCustomerWithTransactionsQueryHandler>.Instance);

        var page1 = (await handler.Handle(
            new GetCustomerWithTransactionsQuery(s.Customer.Id.Value, null, null, null, Page: 1, PageSize: 2), CancellationToken.None)).Value;
        var page3 = (await handler.Handle(
            new GetCustomerWithTransactionsQuery(s.Customer.Id.Value, null, null, null, Page: 3, PageSize: 2), CancellationToken.None)).Value;

        foreach (var page in new[] { page1, page3 })
        {
            page.TotalIncome.Should().Be(1000m);
            page.TotalExpenses.Should().Be(500m);
            page.NetBalance.Should().Be(500m);
            page.PendingIncome.Should().Be(50m);
            page.PendingExpenses.Should().Be(80m);
        }

        page1.Transactions.Select(t => t.TransactionDate).Should().BeInDescendingOrder();
    }

    [Fact]
    public async Task AccountBalance_IsBookedOnly_WithPendingAndAvailableAlongside()
    {
        var s = await SeedAsync();
        var provider = new TransactionBalanceProvider(s.Transactions);

        var single = await provider.GetBalanceAsync(s.Account.Id.Value);
        var byCustomer = (await provider.GetBalancesByCustomerAsync(s.Customer.Id.Value))[s.Account.Id.Value];

        foreach (var balance in new[] { single, byCustomer })
        {
            balance.Booked.Should().Be(500m);
            balance.PendingDebits.Should().Be(-80m);
            balance.PendingCredits.Should().Be(50m);
            balance.Pending.Should().Be(-30m);
            balance.Available.Should().Be(420m, "pending outflows are already unavailable; pending inflows aren't available yet");
        }
    }

    [Fact]
    public async Task AllThreeReadPaths_AgreeOnTheNet()
    {
        var s = await SeedAsync();

        var summary = (await new GetTransactionSummaryQueryHandler(s.Transactions, NullLogger<GetTransactionSummaryQueryHandler>.Instance)
            .Handle(new GetTransactionSummaryQuery(s.Customer.Id.Value, Day.AddDays(-1), Day.AddDays(1)), CancellationToken.None)).Value;
        var customerView = (await new GetCustomerWithTransactionsQueryHandler(
                s.Transactions, new CustomersReadApi(s.Customers), NullLogger<GetCustomerWithTransactionsQueryHandler>.Instance)
            .Handle(new GetCustomerWithTransactionsQuery(s.Customer.Id.Value, null, null, null), CancellationToken.None)).Value;
        var balance = await new TransactionBalanceProvider(s.Transactions).GetBalanceAsync(s.Account.Id.Value);

        summary.NetBalance.Should().Be(customerView.NetBalance).And.Be(balance.Booked);
    }
}