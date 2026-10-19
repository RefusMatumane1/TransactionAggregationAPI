using BuildingBlocks.Application.Abstractions.Authentication;
using FluentAssertions;
using Modules.Customers.Application.Contracts;
using Modules.Customers.Domain;
using TransactionAggregation.Tests.Helpers;
using Xunit;

namespace TransactionAggregation.Tests.Unit.Application.Customers
{
    public class CustomerAccountVisibilityTests
    {
        private readonly string _db = Guid.NewGuid().ToString("N");

        private async Task<Guid> CustomerWithAsync(params (string Bank, string Account)[] accounts)
        {
            using var context = InMemoryCustomersDbContextFactory.Create(_db);
            var customer = Customer.Create($"CUST-{Guid.NewGuid():N}"[..20], "Visible Customer");
            foreach (var (bank, account) in accounts)
                customer.Link(bank, account, DateTime.UtcNow);
            context.Customers.Add(customer);
            await context.SaveChangesAsync();
            return customer.Id.Value;
        }

        private async Task<IReadOnlyList<string>?> VisibleAsync(Guid customer, InstitutionAccess access)
        {
            using var context = InMemoryCustomersDbContextFactory.Create(_db);
            var accounts = await new CustomerAccounts(context).FindVisibleAsync(customer, access);
            return accounts?.Select(a => $"{a.Institution}/{a.ExternalAccountId}").ToList();
        }

        [Fact]
        public async Task AnAdministrator_SeesEveryLinkedAccount_InAStableOrder()
        {
            var customer = await CustomerWithAsync((TestInstitutions.Capitec, "b-2"), (TestInstitutions.FNB, "z-9"), (TestInstitutions.FNB, "a-1"));

            (await VisibleAsync(customer, InstitutionAccess.All)).Should().Equal("Capitec/b-2", "FNB/a-1", "FNB/z-9");
        }

        [Fact]
        public async Task AStaffMember_SeesOnlyTheAccountsAtTheirBanks()
        {
            var customer = await CustomerWithAsync((TestInstitutions.FNB, "a-1"), (TestInstitutions.Capitec, "b-2"));

            (await VisibleAsync(customer, InstitutionAccess.Only([TestInstitutions.Capitec]))).Should().Equal("Capitec/b-2");
        }

        [Fact]
        public async Task TheBankComparison_IgnoresCase()
        {
            var customer = await CustomerWithAsync((TestInstitutions.FNB, "a-1"));

            (await VisibleAsync(customer, InstitutionAccess.Only(["fnb"]))).Should().Equal("FNB/a-1");
        }

        [Fact]
        public async Task ARestrictedCallerWhoCanReadNoneOfTheirBanks_CannotTellTheCustomerFromAMissingOne()
        {
            var customer = await CustomerWithAsync((TestInstitutions.FNB, "a-1"));

            (await VisibleAsync(customer, InstitutionAccess.Only([TestInstitutions.Absa]))).Should().BeNull();
            (await VisibleAsync(customer, InstitutionAccess.None)).Should().BeNull();
            (await VisibleAsync(Guid.NewGuid(), InstitutionAccess.Only([TestInstitutions.Absa]))).Should().BeNull();
        }

        [Fact]
        public async Task AnAdministrator_StillFindsACustomerWithNoLinkedAccounts_AsEmpty()
        {
            var customer = await CustomerWithAsync();

            (await VisibleAsync(customer, InstitutionAccess.All)).Should().BeEmpty();
            (await VisibleAsync(Guid.NewGuid(), InstitutionAccess.All)).Should().BeNull("there is no such customer");
        }
    }
}
