using BuildingBlocks.Application.Abstractions.Authentication;
using BuildingBlocks.Application.Querying;
using FluentAssertions;
using Modules.Customers.Domain;
using Modules.Transactions.Domain.Common.ValueObjects;
using Modules.Transactions.Domain.Entities;
using Xunit;

namespace TransactionAggregation.Tests.Unit.Application.Queries
{
    public class AccountFilterTests
    {
        private sealed record Row(string Bank, string Account);

        private static readonly Row[] Rows =
        [
            new("FNB", "a-1"), new("FNB", "a-2"), new("Absa", "a-1"), new("Capitec", "c-1")
        ];

        private static List<string> Scope(AccountFilter filter) =>
            Rows.AsQueryable()
                .InScope(filter, r => r.Bank, r => r.Account)
                .Select(r => $"{r.Bank}/{r.Account}")
                .ToList();

        [Fact]
        public void AnAdministratorWithNoNarrowing_SeesEverything() =>
            Scope(new AccountFilter(InstitutionAccess.All)).Should().HaveCount(4);

        [Fact]
        public void AStaffMember_SeesOnlyTheirBanks() =>
            Scope(new AccountFilter(InstitutionAccess.Only(["FNB", "Capitec"]))).Should().BeEquivalentTo("FNB/a-1", "FNB/a-2", "Capitec/c-1");

        [Fact]
        public void NoAccess_SeesNothing() =>
            Scope(new AccountFilter(InstitutionAccess.None)).Should().BeEmpty();

        [Fact]
        public void ABankFilter_NarrowsWithinWhatTheCallerMaySee()
        {
            Scope(new AccountFilter(InstitutionAccess.All, Institution: "FNB")).Should().BeEquivalentTo("FNB/a-1", "FNB/a-2");
            Scope(new AccountFilter(InstitutionAccess.Only(["Absa"]), Institution: "FNB")).Should().BeEmpty("asking for a bank is not a way around access");
        }

        [Fact]
        public void AnAccountIdIsOnlyMeaningfulWithItsBank() =>
            Scope(new AccountFilter(InstitutionAccess.All, "FNB", "a-1")).Should().Equal("FNB/a-1");

        [Fact]
        public void AnAccountSet_ChoosesExactlyThoseAccounts_NotEveryBankThatSharesAnId() =>
            Scope(new AccountFilter(InstitutionAccess.All, Accounts: [new AccountKey("FNB", "a-1"), new AccountKey("Capitec", "c-1")]))
                .Should().BeEquivalentTo("FNB/a-1", "Capitec/c-1");

        [Fact]
        public void AnEmptyAccountSet_MeansNoAccounts_NotAllOfThem() =>
            Scope(new AccountFilter(InstitutionAccess.All, Accounts: [])).Should().BeEmpty();

        [Fact]
        public void AnAccountSet_StillRespectsWhatTheCallerMaySee() =>
            Scope(new AccountFilter(InstitutionAccess.Only(["FNB"]), Accounts: [new AccountKey("FNB", "a-1"), new AccountKey("Absa", "a-1")]))
                .Should().Equal("FNB/a-1");

        [Fact]
        public void TheLimits_AgreeWithTheDomainsThatStoreTheseValues()
        {
            AccountFilter.MaxInstitutionLength.Should().Be(TransactionSource.MaxNameLength);
            AccountFilter.MaxExternalAccountIdLength.Should().Be(Transaction.MaxExternalAccountIdLength);
            AccountFilter.MaxExternalAccountIdLength.Should().Be(Customer.MaxExternalAccountIdLength);
        }

        [Fact]
        public void TheValidator_RefusesTooManyAccounts_AndAnAccountWithoutItsBank()
        {
            var validator = new AccountFilterValidator();

            validator.Validate(new AccountFilter(InstitutionAccess.All, Accounts: [.. Enumerable.Range(0, AccountFilter.MaxAccounts + 1)
                .Select(i => new AccountKey("FNB", $"a-{i}"))])).IsValid.Should().BeFalse();
            validator.Validate(new AccountFilter(InstitutionAccess.All, ExternalAccountId: "a-1")).IsValid.Should().BeFalse();
            validator.Validate(new AccountFilter(InstitutionAccess.All, "FNB", "a-1")).IsValid.Should().BeTrue();
        }
    }
}
