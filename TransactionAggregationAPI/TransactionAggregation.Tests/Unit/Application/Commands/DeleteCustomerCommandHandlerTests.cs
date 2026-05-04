using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using TransactionAggregation.Application.Commands.Customer.DeleteCustomer;
using TransactionAggregation.Application.Common.Enums;
using TransactionAggregation.Domain.Common.ValueObjects;
using TransactionAggregation.Domain.Entities;
using TransactionAggregation.Domain.Enums;
using TransactionAggregation.Tests.Helpers;
using Xunit;

namespace TransactionAggregation.Tests.Unit.Application.Commands;

public class DeleteCustomerCommandHandlerTests
{
    private static DeleteCustomerCommandHandler BuildHandler(
        TransactionAggregation.Persistence.ApplicationDbContext ctx)
        => new(ctx, NullLogger<DeleteCustomerCommandHandler>.Instance);

    private static async Task<Customer> SeedCustomerAsync(
        TransactionAggregation.Persistence.ApplicationDbContext ctx,
        string email = "user@example.com")
    {
        var customer = Customer.Create(CustomerId.Create(), email, "Test User", "hashed");
        ctx.Customers.Add(customer);
        await ctx.SaveChangesAsync();
        return customer;
    }

    // ── Happy path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_CustomerWithNoTransactions_DeletesSuccessfully()
    {
        var context = InMemoryDbContextFactory.Create();
        var customer = await SeedCustomerAsync(context);
        var handler = BuildHandler(context);

        var result = await handler.Handle(
            new DeleteCustomerCommand(customer.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        context.Customers.Should().BeEmpty();
    }

    // ── Not found ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_NonExistentCustomer_ReturnsNotFound()
    {
        var context = InMemoryDbContextFactory.Create();
        var handler = BuildHandler(context);

        var result = await handler.Handle(
            new DeleteCustomerCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    // ── Cannot delete with transactions ──────────────────────────────────────

    [Fact]
    public async Task Handle_CustomerWithTransactions_ReturnsValidationFailure()
    {
        var context = InMemoryDbContextFactory.Create();
        var customer = await SeedCustomerAsync(context);
        var transaction = Transaction.Create(
            customer.Id,
            Money.Create(-100m, "ZAR"),
            "test tx",
            TransactionCategory.Uncategorized,
            TransactionSource.Create("TestSource", Guid.NewGuid().ToString()));

        context.Transactions.Add(transaction);
        await context.SaveChangesAsync();

        var handler = BuildHandler(context);
        var result = await handler.Handle(
            new DeleteCustomerCommand(customer.Id.Value), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task Handle_CustomerWithTransactions_DoesNotDeleteCustomer()
    {
        var context = InMemoryDbContextFactory.Create();
        var customer = await SeedCustomerAsync(context);
        var transaction = Transaction.Create(
            customer.Id,
            Money.Create(-50m, "ZAR"),
            "existing tx",
            TransactionCategory.Uncategorized,
            TransactionSource.Create("TestSource", Guid.NewGuid().ToString()));

        context.Transactions.Add(transaction);
        await context.SaveChangesAsync();

        var handler = BuildHandler(context);
        await handler.Handle(new DeleteCustomerCommand(customer.Id.Value), CancellationToken.None);

        context.Customers.Should().HaveCount(1);
    }

    // ── Multiple customers ────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_DeleteOneOfMultipleCustomers_LeavesOthersIntact()
    {
        var context = InMemoryDbContextFactory.Create();
        var c1 = await SeedCustomerAsync(context, "c1@example.com");
        var c2 = await SeedCustomerAsync(context, "c2@example.com");
        var handler = BuildHandler(context);

        await handler.Handle(new DeleteCustomerCommand(c1.Id.Value), CancellationToken.None);

        context.Customers.Should().HaveCount(1);
        context.Customers.Single().Email.Should().Be("c2@example.com");
    }
}
