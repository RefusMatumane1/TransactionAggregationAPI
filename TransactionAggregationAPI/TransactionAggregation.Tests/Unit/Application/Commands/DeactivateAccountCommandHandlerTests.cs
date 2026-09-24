using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Modules.Customers.Application.Features.DeactivateAccount;
using Modules.Customers.Domain;
using Modules.Customers.Domain.ValueObjects;
using Modules.Customers.Infrastructure.Persistence;
using SharedKernel.Common.Enums;
using SharedKernel.Common.ValueObjects;
using TransactionAggregation.Tests.Helpers;
using Xunit;

namespace TransactionAggregation.Tests.Unit.Application.Commands;

public class DeactivateAccountCommandHandlerTests
{
    private static DeactivateAccountCommandHandler BuildHandler(
        CustomersDbContext ctx)
        => new(ctx, NullLogger<DeactivateAccountCommandHandler>.Instance);

    private static async Task<Account> SeedActiveAccountAsync(
        CustomersDbContext ctx)
    {
        var customerId = CustomerId.Create();
        var account = Account.Create(customerId, "ACC-001", "Test Account", AccountType.Checking);
        ctx.Accounts.Add(account);
        await ctx.SaveChangesAsync();
        return account;
    }

    [Fact]
    public async Task Handle_ActiveAccount_DeactivatesSuccessfully()
    {
        var context = InMemoryCustomersDbContextFactory.Create();
        var account = await SeedActiveAccountAsync(context);
        var handler = BuildHandler(context);

        var result = await handler.Handle(
            new DeactivateAccountCommand(account.Id.Value, account.CustomerId.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        context.Accounts.Single().IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ActiveAccount_SetsUpdatedAt()
    {
        var context = InMemoryCustomersDbContextFactory.Create();
        var account = await SeedActiveAccountAsync(context);
        var handler = BuildHandler(context);

        await handler.Handle(
            new DeactivateAccountCommand(account.Id.Value, account.CustomerId.Value), CancellationToken.None);

        context.Accounts.Single().UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_AlreadyInactiveAccount_StillReturnsSuccess()
    {
        var context = InMemoryCustomersDbContextFactory.Create();
        var account = await SeedActiveAccountAsync(context);
        account.Deactivate();
        await context.SaveChangesAsync();
        var handler = BuildHandler(context);

        var result = await handler.Handle(
            new DeactivateAccountCommand(account.Id.Value, account.CustomerId.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        context.Accounts.Single().IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_NonExistentAccount_ReturnsNotFound()
    {
        var context = InMemoryCustomersDbContextFactory.Create();
        var handler = BuildHandler(context);

        var result = await handler.Handle(
            new DeactivateAccountCommand(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Handle_DeactivateOneOfMultipleAccounts_OtherRemainsActive()
    {
        var context = InMemoryCustomersDbContextFactory.Create();
        var customerId = CustomerId.Create();
        var acc1 = Account.Create(customerId, "ACC-001", "First", AccountType.Checking);
        var acc2 = Account.Create(customerId, "ACC-002", "Second", AccountType.Savings);
        context.Accounts.AddRange(acc1, acc2);
        await context.SaveChangesAsync();

        var handler = BuildHandler(context);
        await handler.Handle(new DeactivateAccountCommand(acc1.Id.Value, acc1.CustomerId.Value), CancellationToken.None);

        context.Accounts.Single(a => a.Id == acc1.Id).IsActive.Should().BeFalse();
        context.Accounts.Single(a => a.Id == acc2.Id).IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_AnotherCustomersAccount_ReturnsNotFoundAndLeavesItActive()
    {
        var context = InMemoryCustomersDbContextFactory.Create();
        var account = await SeedActiveAccountAsync(context);
        var handler = BuildHandler(context);

        var result = await handler.Handle(
            new DeactivateAccountCommand(account.Id.Value, Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(Modules.Customers.Application.Errors.AccountErrors.NotFound(account.Id.Value));
        context.Accounts.Single().IsActive.Should().BeTrue();
    }
}