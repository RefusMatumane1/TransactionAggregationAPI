using FluentAssertions;
using Modules.BankLinks.Application.Features.GetBankLinks;
using Modules.BankLinks.Application.Persistence;
using Modules.BankLinks.Domain;
using Modules.BankLinks.Domain.ValueObjects;
using SharedKernel.Common.ValueObjects;
using TransactionAggregation.Tests.Helpers;
using Xunit;

namespace TransactionAggregation.Tests.Unit.Application.Queries;

public class GetBankLinksQueryHandlerTests
{
    private static GetBankLinksQueryHandler BuildHandler(IBankLinksDbContext ctx) => new(ctx);

    [Fact]
    public async Task Handle_CustomerWithNoLinks_ReturnsEmptyList()
    {
        var context = InMemoryBankLinksDbContextFactory.Create();
        var customerId = CustomerId.Create();
        var handler = BuildHandler(context);

        var result = await handler.Handle(new GetBankLinksQuery(customerId.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_CustomerWithLinks_ReturnsOnlyThatCustomersLinks()
    {
        var context = InMemoryBankLinksDbContextFactory.Create();
        var customerId = CustomerId.Create();
        var otherCustomerId = CustomerId.Create();

        var link = BankLink.Create(customerId, Institution.FNB);
        var otherLink = BankLink.Create(otherCustomerId, Institution.Absa);
        context.BankLinks.AddRange(link, otherLink);
        await context.SaveChangesAsync();

        var handler = BuildHandler(context);

        var result = await handler.Handle(new GetBankLinksQuery(customerId.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle();
        result.Value.Single().Id.Should().Be(link.Id.Value);
        result.Value.Single().Institution.Should().Be(Institution.FNB);
    }

    [Fact]
    public async Task Handle_MapsStatusAndAccountIdCorrectly()
    {
        var context = InMemoryBankLinksDbContextFactory.Create();
        var customerId = CustomerId.Create();

        var link = BankLink.Create(customerId, Institution.StandardBank);
        var accountId = AccountId.Create();
        link.Activate(accountId, "ext-1", "enc-access", "enc-refresh", DateTime.UtcNow.AddHours(1));
        context.BankLinks.Add(link);
        await context.SaveChangesAsync();

        var handler = BuildHandler(context);

        var result = await handler.Handle(new GetBankLinksQuery(customerId.Value), CancellationToken.None);

        var dto = result.Value.Single();
        dto.Status.Should().Be(BankLinkStatus.Active);
        dto.AccountId.Should().Be(accountId.Value);
    }

    [Fact]
    public async Task Handle_NoBankLinksTableRows_ForUnknownCustomer_ReturnsEmptyList()
    {
        var context = InMemoryBankLinksDbContextFactory.Create();
        var handler = BuildHandler(context);

        var result = await handler.Handle(new GetBankLinksQuery(Guid.NewGuid()), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }
}
