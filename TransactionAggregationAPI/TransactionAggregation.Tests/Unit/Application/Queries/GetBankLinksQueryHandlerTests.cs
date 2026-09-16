using FluentAssertions;
using TransactionAggregation.Application.Queries.BankLink.GetBankLinks;
using TransactionAggregation.Domain.Common.ValueObjects;
using TransactionAggregation.Domain.Entities;
using TransactionAggregation.Domain.Enums;
using TransactionAggregation.Persistence;
using TransactionAggregation.Tests.Helpers;
using Xunit;

namespace TransactionAggregation.Tests.Unit.Application.Queries;

public class GetBankLinksQueryHandlerTests
{
    private static GetBankLinksQueryHandler BuildHandler(ApplicationDbContext ctx) => new(ctx);

    private static async Task<Customer> SeedCustomerAsync(ApplicationDbContext ctx, string email = "user@example.com")
    {
        var customer = Customer.Create(CustomerId.Create(), email, "Test User");
        ctx.Customers.Add(customer);
        await ctx.SaveChangesAsync();
        return customer;
    }

    [Fact]
    public async Task Handle_CustomerWithNoLinks_ReturnsEmptyList()
    {
        var context = InMemoryDbContextFactory.Create();
        var customer = await SeedCustomerAsync(context);
        var handler = BuildHandler(context);

        var result = await handler.Handle(new GetBankLinksQuery(customer.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_CustomerWithLinks_ReturnsOnlyThatCustomersLinks()
    {
        var context = InMemoryDbContextFactory.Create();
        var customer = await SeedCustomerAsync(context, "c1@example.com");
        var otherCustomer = await SeedCustomerAsync(context, "c2@example.com");

        var link = BankLink.Create(customer.Id, Institution.FNB);
        var otherLink = BankLink.Create(otherCustomer.Id, Institution.Absa);
        context.BankLinks.AddRange(link, otherLink);
        await context.SaveChangesAsync();

        var handler = BuildHandler(context);

        var result = await handler.Handle(new GetBankLinksQuery(customer.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle();
        result.Value.Single().Id.Should().Be(link.Id.Value);
        result.Value.Single().Institution.Should().Be(Institution.FNB);
    }

    [Fact]
    public async Task Handle_MapsStatusAndAccountIdCorrectly()
    {
        var context = InMemoryDbContextFactory.Create();
        var customer = await SeedCustomerAsync(context);

        var link = BankLink.Create(customer.Id, Institution.StandardBank);
        var accountId = AccountId.Create();
        link.Activate(accountId, "ext-1", "enc-access", "enc-refresh", DateTime.UtcNow.AddHours(1));
        context.BankLinks.Add(link);
        await context.SaveChangesAsync();

        var handler = BuildHandler(context);

        var result = await handler.Handle(new GetBankLinksQuery(customer.Id.Value), CancellationToken.None);

        var dto = result.Value.Single();
        dto.Status.Should().Be(BankLinkStatus.Active);
        dto.AccountId.Should().Be(accountId.Value);
    }

    [Fact]
    public async Task Handle_NoBankLinksTableRows_ForUnknownCustomer_ReturnsEmptyList()
    {
        var context = InMemoryDbContextFactory.Create();
        var handler = BuildHandler(context);

        var result = await handler.Handle(new GetBankLinksQuery(Guid.NewGuid()), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }
}
