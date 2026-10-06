using FluentAssertions;
using Modules.Customers.Domain;
using SharedKernel.Exceptions;
using Xunit;

namespace TransactionAggregation.Tests.Unit.Domain
{
    public class CustomerTests
    {
        private static readonly DateTime Now = new(2026, 10, 4, 8, 0, 0, DateTimeKind.Utc);

        [Theory]
        [InlineData("")]
        [InlineData("-CUST")]
        [InlineData("CUST 1")]
        [InlineData("CUST/1")]
        public void Create_RejectsAReferenceThatIsNotACode(string reference)
        {
            var act = () => Customer.Create(reference, "Thandi Nkosi");

            act.Should().Throw<DomainException>();
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void Create_RejectsABlankName(string name)
        {
            var act = () => Customer.Create("CUST-1", name);

            act.Should().Throw<DomainException>();
        }

        [Fact]
        public void Create_TrimsTheName_AndStartsWithNoAccounts()
        {
            var customer = Customer.Create("CUST-1", "  Thandi Nkosi ");

            customer.Name.Should().Be("Thandi Nkosi");
            customer.Accounts.Should().BeEmpty();
        }

        [Fact]
        public void Link_IsIdempotent_PerBankAndAccount()
        {
            var customer = Customer.Create("CUST-1", "Thandi Nkosi");

            customer.Link("FNB", "62001", Now).Should().BeTrue();
            customer.Link("FNB", "62001", Now).Should().BeFalse("the same account linked again changes nothing");
            customer.Link("fnb", "62001", Now).Should().BeFalse("bank codes compare case-insensitively");

            customer.Accounts.Should().ContainSingle();
        }

        [Fact]
        public void Link_TheSameAccountIdAtAnotherBank_IsADifferentAccount()
        {
            var customer = Customer.Create("CUST-1", "Thandi Nkosi");

            customer.Link("FNB", "1001", Now);
            customer.Link("Absa", "1001", Now);

            customer.Accounts.Select(a => a.Institution).Should().BeEquivalentTo(["FNB", "Absa"]);
        }

        [Theory]
        [InlineData("", "62001")]
        [InlineData("F N B", "62001")]
        [InlineData("FNB", "")]
        [InlineData("FNB", " 62001")]
        [InlineData("FNB", "620\n01")]
        public void Link_RejectsAMalformedAccount(string institution, string account)
        {
            var customer = Customer.Create("CUST-1", "Thandi Nkosi");

            var act = () => customer.Link(institution, account, Now);

            act.Should().Throw<DomainException>();
        }

        [Fact]
        public void Link_StopsAtTheLimit_SoACustomerViewStaysABoundedQuery()
        {
            var customer = Customer.Create("CUST-1", "Thandi Nkosi");
            for (var i = 0; i < Customer.MaxLinkedAccounts; i++)
                customer.Link("FNB", $"acc-{i}", Now);

            var act = () => customer.Link("FNB", "one-too-many", Now);

            act.Should().Throw<DomainException>();
            customer.Link("FNB", "acc-0", Now).Should().BeFalse("re-linking an existing account is still allowed at the limit");
        }

        [Fact]
        public void Unlink_RemovesOnlyThatAccount_AndReportsWhenThereWasNone()
        {
            var customer = Customer.Create("CUST-1", "Thandi Nkosi");
            customer.Link("FNB", "62001", Now);
            customer.Link("Capitec", "13001", Now);

            customer.Unlink("FNB", "62001").Should().BeTrue();
            customer.Unlink("FNB", "62001").Should().BeFalse();

            customer.Accounts.Should().ContainSingle().Which.Institution.Should().Be("Capitec");
        }
    }
}