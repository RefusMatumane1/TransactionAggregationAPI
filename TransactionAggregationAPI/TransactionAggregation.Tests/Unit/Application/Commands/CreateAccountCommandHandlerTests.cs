using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using TransactionAggregation.Application.Commands.Account.CreateAccount;
using TransactionAggregation.Application.Common.Enums;
using TransactionAggregation.Domain.Common.ValueObjects;
using TransactionAggregation.Domain.Entities;
using TransactionAggregation.Domain.Enums;
using TransactionAggregation.Tests.Helpers;
using Xunit;

namespace TransactionAggregation.Tests.Unit.Application.Commands;

public class CreateAccountCommandHandlerTests
{
    private static CreateAccountCommandHandler BuildHandler(
        TransactionAggregation.Persistence.ApplicationDbContext ctx)
        => new(ctx, NullLogger<CreateAccountCommandHandler>.Instance);

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
    public async Task Handle_ValidCommand_CreatesAccountAndReturnsId()
    {
        var context = InMemoryDbContextFactory.Create();
        var customer = await SeedCustomerAsync(context);
        var handler = BuildHandler(context);

        var result = await handler.Handle(
            new CreateAccountCommand(
                customer.Id.Value, "ACC-001", "Main Account",
                AccountType.Checking, "ZAR"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Handle_ValidCommand_PersistsAccountInDatabase()
    {
        var context = InMemoryDbContextFactory.Create();
        var customer = await SeedCustomerAsync(context);
        var handler = BuildHandler(context);

        await handler.Handle(
            new CreateAccountCommand(
                customer.Id.Value, "ACC-001", "Main Account",
                AccountType.Savings, "ZAR"),
            CancellationToken.None);

        context.Accounts.Should().HaveCount(1);
        context.Accounts.Single().AccountNumber.Should().Be("ACC-001");
        context.Accounts.Single().AccountName.Should().Be("Main Account");
    }

    [Fact]
    public async Task Handle_ReturnedIdMatchesStoredAccount()
    {
        var context = InMemoryDbContextFactory.Create();
        var customer = await SeedCustomerAsync(context);
        var handler = BuildHandler(context);

        var result = await handler.Handle(
            new CreateAccountCommand(
                customer.Id.Value, "ACC-001", "My Account",
                AccountType.Checking),
            CancellationToken.None);

        var stored = context.Accounts.Single();
        stored.Id.Value.Should().Be(result.Value);
    }

    [Theory]
    [InlineData(AccountType.Checking)]
    [InlineData(AccountType.Savings)]
    [InlineData(AccountType.CreditCard)]
    [InlineData(AccountType.Investment)]
    [InlineData(AccountType.Loan)]
    public async Task Handle_AllAccountTypes_Succeed(AccountType accountType)
    {
        var context = InMemoryDbContextFactory.Create();
        var customer = await SeedCustomerAsync(context);
        var handler = BuildHandler(context);

        var result = await handler.Handle(
            new CreateAccountCommand(
                customer.Id.Value, $"ACC-{accountType}", $"{accountType} Account",
                accountType),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    // ── Customer not found ────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_NonExistentCustomer_ReturnsNotFound()
    {
        var context = InMemoryDbContextFactory.Create();
        var handler = BuildHandler(context);

        var result = await handler.Handle(
            new CreateAccountCommand(
                Guid.NewGuid(), "ACC-001", "My Account",
                AccountType.Checking),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    // ── Duplicate account number ──────────────────────────────────────────────

    [Fact]
    public async Task Handle_DuplicateAccountNumber_ReturnsValidationFailure()
    {
        var context = InMemoryDbContextFactory.Create();
        var customer = await SeedCustomerAsync(context);
        var handler = BuildHandler(context);

        // First account
        await handler.Handle(
            new CreateAccountCommand(
                customer.Id.Value, "ACC-DUP", "First Account",
                AccountType.Checking),
            CancellationToken.None);

        // Duplicate
        var result = await handler.Handle(
            new CreateAccountCommand(
                customer.Id.Value, "ACC-DUP", "Second Account",
                AccountType.Savings),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task Handle_DuplicateAccountNumber_OnlyOneAccountPersisted()
    {
        var context = InMemoryDbContextFactory.Create();
        var customer = await SeedCustomerAsync(context);
        var handler = BuildHandler(context);

        await handler.Handle(
            new CreateAccountCommand(customer.Id.Value, "DUP", "First", AccountType.Checking),
            CancellationToken.None);

        await handler.Handle(
            new CreateAccountCommand(customer.Id.Value, "DUP", "Second", AccountType.Savings),
            CancellationToken.None);

        context.Accounts.Should().HaveCount(1);
    }

    // ── Different customers can share account numbers ─────────────────────────

    [Fact]
    public async Task Handle_SameAccountNumberForDifferentCustomers_BothSucceed()
    {
        var context = InMemoryDbContextFactory.Create();
        var c1 = await SeedCustomerAsync(context, "c1@example.com");
        var c2 = await SeedCustomerAsync(context, "c2@example.com");
        var handler = BuildHandler(context);

        var r1 = await handler.Handle(
            new CreateAccountCommand(c1.Id.Value, "ACC-SHARED", "Account", AccountType.Checking),
            CancellationToken.None);

        var r2 = await handler.Handle(
            new CreateAccountCommand(c2.Id.Value, "ACC-SHARED", "Account", AccountType.Checking),
            CancellationToken.None);

        r1.IsSuccess.Should().BeTrue();
        r2.IsSuccess.Should().BeTrue();
        context.Accounts.Should().HaveCount(2);
    }
}
