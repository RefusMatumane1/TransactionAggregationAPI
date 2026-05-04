using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using TransactionAggregation.Application.Common.Enums;
using TransactionAggregation.Application.Queries.Customer.GetCustomer;
using TransactionAggregation.Persistence;
using TransactionAggregation.Domain.Common.ValueObjects;
using TransactionAggregation.Domain.Entities;
using TransactionAggregation.Domain.Enums;
using TransactionAggregation.Tests.Helpers;
using Xunit;

namespace TransactionAggregation.Tests.Unit.Application.Queries;

public class GetCustomerWithTransactionsQueryHandlerTests
{
    private static Transaction MakeTransaction(
        CustomerId customerId,
        decimal amount,
        TransactionCategory category = TransactionCategory.Uncategorized,
        TransactionStatus status = TransactionStatus.Settled)
    {
        // Transaction.Create always stamps Date = DateTime.UtcNow
        var tx = Transaction.Create(
            customerId,
            Money.Create(amount, "ZAR"),
            "test",
            category,
            TransactionSource.Create("Test", Guid.NewGuid().ToString()));

        tx.UpdateStatus(status, "test");
        return tx;
    }

    private static GetCustomerWithTransactionsQueryHandler BuildHandler(
        ApplicationDbContext context)
        => new(context, NullLogger<GetCustomerWithTransactionsQueryHandler>.Instance);

    // ── Not found ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_CustomerNotFound_ReturnsNotFoundError()
    {
        var context = InMemoryDbContextFactory.Create();
        var handler = BuildHandler(context);

        var result = await handler.Handle(
            new GetCustomerWithTransactionsQuery(Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    // ── TotalTransactions = overall count, not page size (bug fix regression) ─

    [Fact]
    public async Task Handle_TotalTransactionsReflectsAllPages_NotJustCurrentPage()
    {
        var context = InMemoryDbContextFactory.Create();
        var customer = Customer.Create(CustomerId.Create(), "test@example.com", "Test", "hashed");
        context.Customers.Add(customer);

        // Add 25 transactions but request page size = 5
        for (int i = 1; i <= 25; i++)
            context.Transactions.Add(MakeTransaction(customer.Id, -(i * 10m)));

        await context.SaveChangesAsync();

        var handler = BuildHandler(context);
        var result = await handler.Handle(
            new GetCustomerWithTransactionsQuery(customer.Id.Value, Page: 1, PageSize: 5),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalTransactions.Should().Be(25);      // all 25, not 5
        result.Value.Transactions.Should().HaveCount(5);     // only current page
    }

    // ── Category filter ───────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_CategoryFilter_ReturnsOnlyMatchingTransactions()
    {
        var context = InMemoryDbContextFactory.Create();
        var customer = Customer.Create(CustomerId.Create(), "test@example.com", "Test", "hashed");
        context.Customers.Add(customer);

        context.Transactions.AddRange(
            MakeTransaction(customer.Id, -50m,   TransactionCategory.Groceries),
            MakeTransaction(customer.Id, -30m,   TransactionCategory.Groceries),
            MakeTransaction(customer.Id, -20m,   TransactionCategory.Transportation),
            MakeTransaction(customer.Id, 2500m,  TransactionCategory.Income));

        await context.SaveChangesAsync();

        var handler = BuildHandler(context);
        var result = await handler.Handle(
            new GetCustomerWithTransactionsQuery(
                customer.Id.Value, Category: TransactionCategory.Groceries),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalTransactions.Should().Be(2);
        result.Value.Transactions.Should().AllSatisfy(
            t => t.Category.Should().Be(TransactionCategory.Groceries));
    }

    // ── Date filter ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_DateRangeIncludingNow_ReturnsAllTransactions()
    {
        var context = InMemoryDbContextFactory.Create();
        var customer = Customer.Create(CustomerId.Create(), "date@example.com", "Date Test", "hashed");
        context.Customers.Add(customer);

        // Transaction.Create stamps Date = UtcNow, so a range around now includes them
        context.Transactions.AddRange(
            MakeTransaction(customer.Id, -100m),
            MakeTransaction(customer.Id, -200m));

        await context.SaveChangesAsync();

        var handler = BuildHandler(context);
        var result = await handler.Handle(
            new GetCustomerWithTransactionsQuery(
                customer.Id.Value,
                StartDate: DateTime.UtcNow.AddMinutes(-1),
                EndDate:   DateTime.UtcNow.AddMinutes(1)),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalTransactions.Should().Be(2);
    }

    [Fact]
    public async Task Handle_DateRangeInPast_ExcludesAllCurrentTransactions()
    {
        var context = InMemoryDbContextFactory.Create();
        var customer = Customer.Create(CustomerId.Create(), "past@example.com", "Past Test", "hashed");
        context.Customers.Add(customer);
        context.Transactions.Add(MakeTransaction(customer.Id, -100m)); // stamped UtcNow
        await context.SaveChangesAsync();

        var handler = BuildHandler(context);
        var result = await handler.Handle(
            new GetCustomerWithTransactionsQuery(
                customer.Id.Value,
                StartDate: DateTime.UtcNow.AddDays(-30),
                EndDate:   DateTime.UtcNow.AddDays(-1)),  // ends before now
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalTransactions.Should().Be(0);
    }

    // ── Income / expense summary ──────────────────────────────────────────────

    [Fact]
    public async Task Handle_SummaryCalculatesIncomeAndExpenses_ForSettledTransactions()
    {
        var context = InMemoryDbContextFactory.Create();
        var customer = Customer.Create(CustomerId.Create(), "fin@example.com", "Finance", "hashed");
        context.Customers.Add(customer);

        context.Transactions.AddRange(
            MakeTransaction(customer.Id,  3000m, status: TransactionStatus.Settled),
            MakeTransaction(customer.Id, -1000m, status: TransactionStatus.Settled),
            MakeTransaction(customer.Id,  -500m, status: TransactionStatus.Settled),
            // Pending transaction should NOT be counted in totals
            MakeTransaction(customer.Id,  -200m, status: TransactionStatus.Pending));

        await context.SaveChangesAsync();

        var handler = BuildHandler(context);
        var result = await handler.Handle(
            new GetCustomerWithTransactionsQuery(customer.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalIncome.Should().Be(3000m);
        result.Value.TotalExpenses.Should().Be(1500m);
        result.Value.NetBalance.Should().Be(1500m);
    }

    // ── Returns customer metadata alongside transactions ───────────────────────

    [Fact]
    public async Task Handle_ReturnsCorrectCustomerInfo()
    {
        var context = InMemoryDbContextFactory.Create();
        var customer = Customer.Create(CustomerId.Create(), "info@example.com", "Info User", "hashed");
        context.Customers.Add(customer);
        await context.SaveChangesAsync();

        var handler = BuildHandler(context);
        var result = await handler.Handle(
            new GetCustomerWithTransactionsQuery(customer.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(customer.Id.Value);
        result.Value.Email.Should().Be("info@example.com");
        result.Value.Name.Should().Be("Info User");
    }
}
