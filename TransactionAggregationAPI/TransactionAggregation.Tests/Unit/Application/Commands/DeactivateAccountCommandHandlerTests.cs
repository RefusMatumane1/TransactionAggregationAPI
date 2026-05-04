using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using TransactionAggregation.Application.Commands.Account.DeactivateAccount;
using TransactionAggregation.Application.Common.Enums;
using TransactionAggregation.Domain.Common.ValueObjects;
using TransactionAggregation.Domain.Entities;
using TransactionAggregation.Domain.Enums;
using TransactionAggregation.Tests.Helpers;
using Xunit;

namespace TransactionAggregation.Tests.Unit.Application.Commands;

public class DeactivateAccountCommandHandlerTests
{
    private static DeactivateAccountCommandHandler BuildHandler(
        TransactionAggregation.Persistence.ApplicationDbContext ctx)
        => new(ctx, NullLogger<DeactivateAccountCommandHandler>.Instance);

    private static async Task<Account> SeedActiveAccountAsync(
        TransactionAggregation.Persistence.ApplicationDbContext ctx)
    {
        var customerId = CustomerId.Create();
        var account = Account.Create(customerId, "ACC-001", "Test Account", AccountType.Checking);
        ctx.Accounts.Add(account);
        await ctx.SaveChangesAsync();
        return account;
    }

    // ── Happy path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ActiveAccount_DeactivatesSuccessfully()
    {
        var context = InMemoryDbContextFactory.Create();
        var account = await SeedActiveAccountAsync(context);
        var handler = BuildHandler(context);

        var result = await handler.Handle(
            new DeactivateAccountCommand(account.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        context.Accounts.Single().IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ActiveAccount_SetsUpdatedAt()
    {
        var context = InMemoryDbContextFactory.Create();
        var account = await SeedActiveAccountAsync(context);
        var handler = BuildHandler(context);

        await handler.Handle(
            new DeactivateAccountCommand(account.Id.Value), CancellationToken.None);

        context.Accounts.Single().UpdatedAt.Should().NotBeNull();
    }

    // ── Idempotency ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_AlreadyInactiveAccount_StillReturnsSuccess()
    {
        var context = InMemoryDbContextFactory.Create();
        var account = await SeedActiveAccountAsync(context);
        account.Deactivate();
        await context.SaveChangesAsync();
        var handler = BuildHandler(context);

        var result = await handler.Handle(
            new DeactivateAccountCommand(account.Id.Value), CancellationToken.None);

        // Deactivating an already-inactive account is idempotent
        result.IsSuccess.Should().BeTrue();
        context.Accounts.Single().IsActive.Should().BeFalse();
    }

    // ── Not found ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_NonExistentAccount_ReturnsNotFound()
    {
        var context = InMemoryDbContextFactory.Create();
        var handler = BuildHandler(context);

        var result = await handler.Handle(
            new DeactivateAccountCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    // ── Multiple accounts ─────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_DeactivateOneOfMultipleAccounts_OtherRemainsActive()
    {
        var context = InMemoryDbContextFactory.Create();
        var customerId = CustomerId.Create();
        var acc1 = Account.Create(customerId, "ACC-001", "First", AccountType.Checking);
        var acc2 = Account.Create(customerId, "ACC-002", "Second", AccountType.Savings);
        context.Accounts.AddRange(acc1, acc2);
        await context.SaveChangesAsync();

        var handler = BuildHandler(context);
        await handler.Handle(new DeactivateAccountCommand(acc1.Id.Value), CancellationToken.None);

        context.Accounts.Single(a => a.Id == acc1.Id).IsActive.Should().BeFalse();
        context.Accounts.Single(a => a.Id == acc2.Id).IsActive.Should().BeTrue();
    }
}
