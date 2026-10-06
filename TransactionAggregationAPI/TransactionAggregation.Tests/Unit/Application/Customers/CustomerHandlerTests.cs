using BuildingBlocks.Application.Abstractions.Authentication;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Modules.Audit.Contracts;
using Modules.Customers.Application.Contracts;
using Modules.Customers.Application.Features.CreateCustomer;
using Modules.Customers.Application.Features.GetCustomer;
using Modules.Customers.Application.Features.GetCustomers;
using Modules.Customers.Application.Features.LinkAccount;
using Modules.Customers.Application.Features.UnlinkAccount;
using Modules.Customers.Domain;
using Modules.Customers.Infrastructure.Persistence;
using Modules.WebhookSources.Contracts;
using NSubstitute;
using SharedKernel.Common.Enums;
using SharedKernel.Common.Models;
using TransactionAggregation.Tests.Helpers;
using Xunit;

namespace TransactionAggregation.Tests.Unit.Application.Customers
{
    public class CustomerHandlerTests
    {
        private static readonly Guid Admin = Guid.NewGuid();

        private readonly RecordingAuditTrail _audit = new();
        private readonly string _db = Guid.NewGuid().ToString("N");

        private CustomersDbContext Context() => InMemoryCustomersDbContextFactory.Create(_db, _audit);

        private static IUserContext User(InstitutionAccess? access = null)
        {
            var user = Substitute.For<IUserContext>();
            user.UserId.Returns(Admin);
            user.InstitutionAccess.Returns(access ?? InstitutionAccess.All);
            return user;
        }

        // The registered banks, as WebhookSources reports them: lookups are case-insensitive.
        private static IWebhookSourceDirectory Banks(params string[] codes)
        {
            var banks = Substitute.For<IWebhookSourceDirectory>();
            banks.FindBankCodeAsync(default!, default).ReturnsForAnyArgs(call =>
                codes.FirstOrDefault(c => string.Equals(c, call.Arg<string>(), StringComparison.OrdinalIgnoreCase)));
            return banks;
        }

        private async Task<Guid> SeedAsync(string reference, params (string Bank, string Account)[] accounts)
        {
            using var context = Context();
            var customer = Customer.Create(reference, $"Customer {reference}");
            foreach (var (bank, account) in accounts)
                customer.Link(bank, account, DateTime.UtcNow);
            context.Customers.Add(customer);
            await context.SaveChangesAsync();
            return customer.Id.Value;
        }

        private Task<Result<LinkAccountResult>> LinkAsync(Guid customer, string bank, string account) =>
            new LinkAccountCommandHandler(Context(), Banks(TestInstitutions.FNB, TestInstitutions.Capitec), User(), TimeProvider.System,
                    NullLogger<LinkAccountCommandHandler>.Instance)
                .Handle(new LinkAccountCommand(customer, bank, account), CancellationToken.None);

        [Fact]
        public async Task Create_RejectsAReferenceAlreadyInUse()
        {
            await SeedAsync("CUST-1");

            var result = await new CreateCustomerCommandHandler(Context(), User(), NullLogger<CreateCustomerCommandHandler>.Instance)
                .Handle(new CreateCustomerCommand("CUST-1", "Someone Else"), CancellationToken.None);

            result.Error.Type.Should().Be(ErrorType.Conflict);
        }

        [Fact]
        public async Task Create_IsAudited_WithoutTheCustomersName()
        {
            var result = await new CreateCustomerCommandHandler(Context(), User(), NullLogger<CreateCustomerCommandHandler>.Instance)
                .Handle(new CreateCustomerCommand("CUST-9", "Thandi Nkosi"), CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            var entry = _audit.Recorded.Should().ContainSingle().Subject;
            entry.EventType.Should().Be(AuditEventTypes.CustomerCreated);
            entry.Actor.Should().Be(Admin.ToString());
            entry.Metadata!["customerReference"].Should().Be("CUST-9");
            string.Join(' ', entry.Detail, string.Join(' ', entry.Metadata.Values)).Should().NotContain("Thandi", "the name is personal data");
        }

        [Fact]
        public async Task Link_StoresTheBanksRegisteredCode_SoTheLinkMatchesItsTransactions()
        {
            var customer = await SeedAsync("CUST-1");

            var result = await LinkAsync(customer, "fnb", "62001");

            result.Value.Linked.Should().BeTrue();
            result.Value.Customer.Accounts.Should().ContainSingle().Which.Institution.Should().Be(TestInstitutions.FNB);
            _audit.Recorded.Should().ContainSingle(e => e.EventType == AuditEventTypes.CustomerAccountLinked && e.SourceName == TestInstitutions.FNB);
        }

        [Fact]
        public async Task Link_ToABankThatIsNotRegistered_IsAValidationError()
        {
            var customer = await SeedAsync("CUST-1");

            var result = await LinkAsync(customer, "NoSuchBank", "62001");

            result.Error.Type.Should().Be(ErrorType.Validation);
        }

        [Fact]
        public async Task Link_Twice_ChangesNothing_AndIsAuditedOnce()
        {
            var customer = await SeedAsync("CUST-1");

            (await LinkAsync(customer, TestInstitutions.FNB, "62001")).Value.Linked.Should().BeTrue();
            var again = await LinkAsync(customer, TestInstitutions.FNB, "62001");

            again.IsSuccess.Should().BeTrue();
            again.Value.Linked.Should().BeFalse();
            again.Value.Customer.Accounts.Should().ContainSingle();
            _audit.Recorded.Count(e => e.EventType == AuditEventTypes.CustomerAccountLinked).Should().Be(1);
        }

        [Fact]
        public async Task Link_ToAnUnknownCustomer_IsNotFound()
        {
            var result = await LinkAsync(Guid.NewGuid(), TestInstitutions.FNB, "62001");

            result.Error.Type.Should().Be(ErrorType.NotFound);
        }

        [Fact]
        public async Task Unlink_RemovesTheLink_ThenReportsNotFound()
        {
            var customer = await SeedAsync("CUST-1", (TestInstitutions.FNB, "62001"));
            var handler = () => new UnlinkAccountCommandHandler(Context(), User(), NullLogger<UnlinkAccountCommandHandler>.Instance);

            (await handler().Handle(new UnlinkAccountCommand(customer, "fnb", "62001"), CancellationToken.None)).IsSuccess.Should().BeTrue();
            var again = await handler().Handle(new UnlinkAccountCommand(customer, TestInstitutions.FNB, "62001"), CancellationToken.None);

            again.Error.Type.Should().Be(ErrorType.NotFound);
            _audit.Recorded.Should().ContainSingle(e => e.EventType == AuditEventTypes.CustomerAccountUnlinked);
        }

        [Fact]
        public async Task GetCustomer_ForStaff_ListsOnlyAccountsAtTheirBanks()
        {
            var customer = await SeedAsync("CUST-1", (TestInstitutions.FNB, "62001"), (TestInstitutions.Capitec, "13001"));

            var result = await new GetCustomerQueryHandler(Context())
                .Handle(new GetCustomerQuery(customer, InstitutionAccess.Only([TestInstitutions.FNB])), CancellationToken.None);

            result.Value.Accounts.Should().ContainSingle().Which.Institution.Should().Be(TestInstitutions.FNB);
        }

        [Fact]
        public async Task GetCustomer_ForStaffWithNoneOfItsBanks_IsNotFound_ExactlyLikeAMissingOne()
        {
            var customer = await SeedAsync("CUST-1", (TestInstitutions.Capitec, "13001"));
            var staff = InstitutionAccess.Only([TestInstitutions.FNB]);

            var hidden = await new GetCustomerQueryHandler(Context()).Handle(new GetCustomerQuery(customer, staff), CancellationToken.None);
            var missing = await new GetCustomerQueryHandler(Context()).Handle(new GetCustomerQuery(Guid.NewGuid(), staff), CancellationToken.None);

            hidden.Error.Type.Should().Be(ErrorType.NotFound);
            hidden.Error.Code.Should().Be(missing.Error.Code);
        }

        [Fact]
        public async Task GetCustomers_ForStaff_ListsOnlyCustomersWithAnAccountAtTheirBanks_AndSearches()
        {
            await SeedAsync("CUST-1", (TestInstitutions.FNB, "62001"), (TestInstitutions.Capitec, "13001"));
            await SeedAsync("CUST-2", (TestInstitutions.Capitec, "13002"));
            await SeedAsync("CUST-3", (TestInstitutions.FNB, "62003"));
            var handler = new GetCustomersQueryHandler(Context(), new InMemoryKeysetPaginator());
            var staff = InstitutionAccess.Only([TestInstitutions.FNB]);

            var all = (await handler.Handle(new GetCustomersQuery(staff), CancellationToken.None)).Value;
            var searched = (await handler.Handle(new GetCustomersQuery(staff, Search: "cust-3"), CancellationToken.None)).Value;

            all.Items.Select(c => c.Reference).Should().Equal("CUST-1", "CUST-3");
            all.Items[0].Institutions.Should().Equal(TestInstitutions.FNB);
            all.Items[0].AccountCount.Should().Be(1, "the Capitec account is not the staff member's to see");
            searched.Items.Should().ContainSingle().Which.Reference.Should().Be("CUST-3");
        }

        [Fact]
        public async Task CustomerAccounts_ResolvesEveryLink_OrNullForAnUnknownCustomer()
        {
            var customer = await SeedAsync("CUST-1", (TestInstitutions.FNB, "62001"), (TestInstitutions.Capitec, "13001"));
            var contract = new CustomerAccounts(Context());

            var accounts = await contract.FindAsync(customer);
            var unknown = await contract.FindAsync(Guid.NewGuid());

            accounts!.Select(a => (a.Institution, a.ExternalAccountId)).Should().BeEquivalentTo(
                [(TestInstitutions.FNB, "62001"), (TestInstitutions.Capitec, "13001")]);
            unknown.Should().BeNull();
        }
    }
}