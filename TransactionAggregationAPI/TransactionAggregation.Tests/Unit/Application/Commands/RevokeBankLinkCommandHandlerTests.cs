using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using TransactionAggregation.Application.Commands.BankLink.RevokeBankLink;
using TransactionAggregation.Application.Common.Enums;
using TransactionAggregation.Domain.Common.ValueObjects;
using TransactionAggregation.Domain.Entities;
using TransactionAggregation.Domain.Enums;
using TransactionAggregation.Persistence;
using TransactionAggregation.Tests.Helpers;
using Xunit;

namespace TransactionAggregation.Tests.Unit.Application.Commands;

public class RevokeBankLinkCommandHandlerTests
{
    private static RevokeBankLinkCommandHandler BuildHandler(ApplicationDbContext ctx)
        => new(ctx, NullLogger<RevokeBankLinkCommandHandler>.Instance);

    private static async Task<Customer> SeedCustomerAsync(ApplicationDbContext ctx, string email = "user@example.com")
    {
        var customer = Customer.Create(CustomerId.Create(), email, "Test User");
        ctx.Customers.Add(customer);
        await ctx.SaveChangesAsync();
        return customer;
    }

    private static async Task<BankLink> SeedActiveLinkAsync(ApplicationDbContext ctx, CustomerId customerId)
    {
        var link = BankLink.Create(customerId, Institution.FNB);
        link.Activate(AccountId.Create(), "ext-1", "enc-access", "enc-refresh", DateTime.UtcNow.AddHours(1));
        ctx.BankLinks.Add(link);
        await ctx.SaveChangesAsync();
        return link;
    }

    [Fact]
    public async Task Handle_OwnedActiveLink_RevokesAndPersists()
    {
        var context = InMemoryDbContextFactory.Create();
        var customer = await SeedCustomerAsync(context);
        var link = await SeedActiveLinkAsync(context, customer.Id);
        var handler = BuildHandler(context);

        var result = await handler.Handle(
            new RevokeBankLinkCommand(customer.Id.Value, link.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        context.BankLinks.Single().Status.Should().Be(BankLinkStatus.Revoked);
    }

    [Fact]
    public async Task Handle_OwnedLink_ClearsTokensOnRevoke()
    {
        var context = InMemoryDbContextFactory.Create();
        var customer = await SeedCustomerAsync(context);
        var link = await SeedActiveLinkAsync(context, customer.Id);
        var handler = BuildHandler(context);

        await handler.Handle(new RevokeBankLinkCommand(customer.Id.Value, link.Id.Value), CancellationToken.None);

        var stored = context.BankLinks.Single();
        stored.EncryptedAccessToken.Should().BeNull();
        stored.EncryptedRefreshToken.Should().BeNull();
    }

    [Fact]
    public async Task Handle_NonExistentBankLink_ReturnsNotFound()
    {
        var context = InMemoryDbContextFactory.Create();
        var customer = await SeedCustomerAsync(context);
        var handler = BuildHandler(context);

        var result = await handler.Handle(
            new RevokeBankLinkCommand(customer.Id.Value, Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Handle_BankLinkOwnedByAnotherCustomer_ReturnsNotFound()
    {
        var context = InMemoryDbContextFactory.Create();
        var owner = await SeedCustomerAsync(context, "owner@example.com");
        var attacker = await SeedCustomerAsync(context, "attacker@example.com");
        var link = await SeedActiveLinkAsync(context, owner.Id);
        var handler = BuildHandler(context);

        var result = await handler.Handle(
            new RevokeBankLinkCommand(attacker.Id.Value, link.Id.Value), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        context.BankLinks.Single().Status.Should().Be(BankLinkStatus.Active);
    }
}
