using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using TransactionAggregation.Application.Commands.Customer.UpdateCustomer;
using TransactionAggregation.Application.Common.Enums;
using TransactionAggregation.Domain.Common.ValueObjects;
using TransactionAggregation.Domain.Entities;
using TransactionAggregation.Tests.Helpers;
using Xunit;

namespace TransactionAggregation.Tests.Unit.Application.Commands;

public class UpdateCustomerCommandHandlerTests
{
    private static UpdateCustomerCommandHandler BuildHandler(
        TransactionAggregation.Persistence.ApplicationDbContext ctx)
        => new(ctx, NullLogger<UpdateCustomerCommandHandler>.Instance);

    private static async Task<Customer> SeedCustomerAsync(
        TransactionAggregation.Persistence.ApplicationDbContext ctx,
        string email, string name)
    {
        var customer = Customer.Create(CustomerId.Create(), email, name, "hashed");
        ctx.Customers.Add(customer);
        await ctx.SaveChangesAsync();
        return customer;
    }

    // ── Happy path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ValidUpdate_UpdatesEmailAndName()
    {
        var context = InMemoryDbContextFactory.Create();
        var customer = await SeedCustomerAsync(context, "old@example.com", "Old Name");
        var handler = BuildHandler(context);

        var result = await handler.Handle(
            new UpdateCustomerCommand(customer.Id.Value, "new@example.com", "New Name"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var stored = context.Customers.Single();
        stored.Email.Should().Be("new@example.com");
        stored.Name.Should().Be("New Name");
    }

    [Fact]
    public async Task Handle_ValidUpdate_SetsUpdatedAt()
    {
        var context = InMemoryDbContextFactory.Create();
        var customer = await SeedCustomerAsync(context, "user@example.com", "User");
        var handler = BuildHandler(context);

        await handler.Handle(
            new UpdateCustomerCommand(customer.Id.Value, "user2@example.com", "User 2"),
            CancellationToken.None);

        context.Customers.Single().UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_SameEmail_UpdatesNameSuccessfully()
    {
        var context = InMemoryDbContextFactory.Create();
        var customer = await SeedCustomerAsync(context, "same@example.com", "Old Name");
        var handler = BuildHandler(context);

        var result = await handler.Handle(
            new UpdateCustomerCommand(customer.Id.Value, "same@example.com", "New Name"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        context.Customers.Single().Name.Should().Be("New Name");
    }

    // ── Not found ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_NonExistentCustomer_ReturnsNotFound()
    {
        var context = InMemoryDbContextFactory.Create();
        var handler = BuildHandler(context);

        var result = await handler.Handle(
            new UpdateCustomerCommand(Guid.NewGuid(), "x@example.com", "X"),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    // ── Email conflict ────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_EmailAlreadyUsedByOtherCustomer_ReturnsConflict()
    {
        var context = InMemoryDbContextFactory.Create();
        await SeedCustomerAsync(context, "taken@example.com", "Existing User");
        var customerToUpdate = await SeedCustomerAsync(context, "other@example.com", "Another User");
        var handler = BuildHandler(context);

        var result = await handler.Handle(
            new UpdateCustomerCommand(customerToUpdate.Id.Value, "taken@example.com", "Another User"),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public async Task Handle_EmailConflict_DoesNotPersistChanges()
    {
        var context = InMemoryDbContextFactory.Create();
        await SeedCustomerAsync(context, "taken@example.com", "Existing");
        var customerToUpdate = await SeedCustomerAsync(context, "original@example.com", "Original Name");
        var handler = BuildHandler(context);

        await handler.Handle(
            new UpdateCustomerCommand(customerToUpdate.Id.Value, "taken@example.com", "New Name"),
            CancellationToken.None);

        // Name should remain unchanged
        context.Customers
            .Single(c => c.Email == "original@example.com")
            .Name.Should().Be("Original Name");
    }
}
