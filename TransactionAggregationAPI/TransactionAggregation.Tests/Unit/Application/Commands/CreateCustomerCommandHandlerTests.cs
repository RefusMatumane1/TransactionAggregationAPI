using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TransactionAggregation.Application.Abstractions.Authentication;
using TransactionAggregation.Application.Commands.Customer.CreateCustomer;
using TransactionAggregation.Application.Common.Enums;
using TransactionAggregation.Domain.Common.ValueObjects;
using TransactionAggregation.Domain.Entities;
using TransactionAggregation.Tests.Helpers;
using Xunit;

namespace TransactionAggregation.Tests.Unit.Application.Commands;

public class CreateCustomerCommandHandlerTests
{
    private static CreateCustomerCommandHandler BuildHandler(
        TransactionAggregation.Persistence.ApplicationDbContext? ctx = null)
    {
        ctx ??= InMemoryDbContextFactory.Create();
        var passwordHasher = Substitute.For<IPasswordHasher>();
        passwordHasher.Hash(Arg.Any<string>()).Returns("hashed");
        return new CreateCustomerCommandHandler(ctx, passwordHasher,
            NullLogger<CreateCustomerCommandHandler>.Instance);
    }

    // ── Happy path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ValidCommand_CreatesCustomerAndReturnsId()
    {
        var context = InMemoryDbContextFactory.Create();
        var handler = BuildHandler(context);
        var command = new CreateCustomerCommand("alice@example.com", "Alice Smith", "password");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Handle_ValidCommand_PersistsCustomerInDatabase()
    {
        var context = InMemoryDbContextFactory.Create();
        var handler = BuildHandler(context);
        var command = new CreateCustomerCommand("bob@example.com", "Bob Jones", "password");

        await handler.Handle(command, CancellationToken.None);

        context.Customers.Should().HaveCount(1);
        context.Customers.Single().Email.Should().Be("bob@example.com");
        context.Customers.Single().Name.Should().Be("Bob Jones");
    }

    [Fact]
    public async Task Handle_ValidCommand_ReturnedIdMatchesStoredCustomer()
    {
        var context = InMemoryDbContextFactory.Create();
        var handler = BuildHandler(context);
        var command = new CreateCustomerCommand("carol@example.com", "Carol White", "password");

        var result = await handler.Handle(command, CancellationToken.None);

        var stored = context.Customers.Single();
        stored.Id.Value.Should().Be(result.Value);
    }

    // ── Duplicate email ───────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_DuplicateEmail_ReturnsConflictFailure()
    {
        var context = InMemoryDbContextFactory.Create();
        var existing = Customer.Create(CustomerId.Create(), "dup@example.com", "Existing User", "hashed");
        context.Customers.Add(existing);
        await context.SaveChangesAsync();

        var handler = BuildHandler(context);
        var command = new CreateCustomerCommand("dup@example.com", "New User", "password");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public async Task Handle_DuplicateEmail_DoesNotPersistSecondCustomer()
    {
        var context = InMemoryDbContextFactory.Create();
        var existing = Customer.Create(CustomerId.Create(), "dup@example.com", "User A", "hashed");
        context.Customers.Add(existing);
        await context.SaveChangesAsync();

        var handler = BuildHandler(context);
        await handler.Handle(new CreateCustomerCommand("dup@example.com", "User B", "password"), CancellationToken.None);

        context.Customers.Should().HaveCount(1);
    }

    // ── Multiple customers ────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_TwoDistinctEmails_BothSucceed()
    {
        var context = InMemoryDbContextFactory.Create();
        var handler = BuildHandler(context);

        await handler.Handle(new CreateCustomerCommand("a@example.com", "Alice", "password"), CancellationToken.None);
        var result = await handler.Handle(new CreateCustomerCommand("b@example.com", "Bob", "password"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        context.Customers.Should().HaveCount(2);
    }
}
