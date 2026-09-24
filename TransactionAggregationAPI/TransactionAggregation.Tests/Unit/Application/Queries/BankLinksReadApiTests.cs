using FluentAssertions;
using Modules.BankLinks.Application.Contracts;
using Modules.BankLinks.Domain;
using Modules.BankLinks.Domain.ValueObjects;
using SharedKernel.Common.ValueObjects;
using TransactionAggregation.Tests.Helpers;
using Xunit;

namespace TransactionAggregation.Tests.Unit.Application.Queries;

public class BankLinksReadApiTests
{
    private static BankLink ActiveLink(string externalAccountId)
    {
        var link = BankLink.Create(CustomerId.Create(), Institution.FNB);
        link.Activate(AccountId.Create(), externalAccountId, "enc-a", "enc-r", DateTime.UtcNow.AddHours(1));
        return link;
    }

    [Fact]
    public async Task FindActiveLinks_ReturnsEveryActiveLinkToTheAccount_OldestFirst()
    {
        var context = InMemoryBankLinksDbContextFactory.Create();
        var newest = ActiveLink("joint");
        var oldest = ActiveLink("joint");
        var middle = ActiveLink("joint");
        context.BankLinks.AddRange(newest, oldest, middle); // inserted out of age order on purpose
        await context.SaveChangesAsync();

        // Saving an insert stamps CreatedAt with "now", so the ages are set on a second save.
        newest.CreatedAt = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        oldest.CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        middle.CreatedAt = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        await context.SaveChangesAsync();

        var links = await new BankLinksReadApi(context).FindActiveLinksByExternalAccountIdAsync("joint");

        links.Select(l => l.BankLinkId).Should().Equal(oldest.Id.Value, middle.Id.Value, newest.Id.Value);
    }

    [Fact]
    public async Task FindActiveLinks_IgnoresInactiveLinksAndOtherAccounts()
    {
        var context = InMemoryBankLinksDbContextFactory.Create();
        var active = ActiveLink("joint");
        var revoked = ActiveLink("joint");
        revoked.Revoke();
        var pending = BankLink.Create(CustomerId.Create(), Institution.Absa);
        var otherAccount = ActiveLink("someone-else");
        context.BankLinks.AddRange(active, revoked, pending, otherAccount);
        await context.SaveChangesAsync();

        var links = await new BankLinksReadApi(context).FindActiveLinksByExternalAccountIdAsync("joint");

        links.Should().ContainSingle().Which.BankLinkId.Should().Be(active.Id.Value);
    }

    [Fact]
    public async Task FindActiveLinks_UnlinkedAccount_ReturnsEmpty()
    {
        var context = InMemoryBankLinksDbContextFactory.Create();

        var links = await new BankLinksReadApi(context).FindActiveLinksByExternalAccountIdAsync("nobody");

        links.Should().BeEmpty();
    }
}