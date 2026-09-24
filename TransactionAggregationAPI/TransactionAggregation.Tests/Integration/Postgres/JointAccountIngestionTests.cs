using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Modules.BankLinks.Application.Contracts;
using Modules.BankLinks.Domain;
using Modules.BankLinks.Domain.ValueObjects;
using Modules.Customers.Domain;
using Modules.Customers.Domain.ValueObjects;
using Modules.Transactions.Application.Common.DTOs;
using Modules.Transactions.Application.Features.Transactions.Commands.ProcessInboundTransactions;
using SharedKernel.Common.ValueObjects;
using Xunit;

namespace TransactionAggregation.Tests.Integration.Postgres
{
    /// <summary>
    /// Against the real schema: two holders of one joint account each get their own row for the same
    /// bank transaction, because the unique index is per customer (CustomerId, SourceName, external id).
    /// </summary>
    [Collection(PostgresCollection.Name)]
    public class JointAccountIngestionTests
    {
        private readonly PostgresContainerFixture _fixture;

        public JointAccountIngestionTests(PostgresContainerFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task Handle_JointAccount_StoresOneRowPerHolder_AndARedeliveryAddsNothing()
        {
            var jointAccount = $"joint-acc-{Guid.NewGuid():N}";
            var alice = await LinkHolderAsync("Alice", jointAccount);
            var bob = await LinkHolderAsync("Bob", jointAccount);
            var delivery = new ProcessInboundTransactionsCommand("joint-test", jointAccount,
            [
                new ExternalTransactionDTO
                {
                    Id = "shared-t1", Amount = -250m, Currency = "ZAR", Description = "Household groceries",
                    Category = string.Empty, Date = DateTime.UtcNow
                }
            ]);

            var first = await DeliverAsync(delivery);
            var redelivery = await DeliverAsync(delivery);

            first.IsSuccess.Should().BeTrue();
            first.Value.Should().Be(2);
            redelivery.IsSuccess.Should().BeTrue();
            redelivery.Value.Should().Be(0);

            using var verify = _fixture.CreateContext();
            var rows = await verify.Transactions
                .Where(t => t.Source.ExternalId == "shared-t1" && (t.CustomerId == alice.CustomerId || t.CustomerId == bob.CustomerId))
                .ToListAsync();
            rows.Should().HaveCount(2);
            rows.Should().ContainSingle(t => t.CustomerId == alice.CustomerId && t.AccountId == alice.AccountId);
            rows.Should().ContainSingle(t => t.CustomerId == bob.CustomerId && t.AccountId == bob.AccountId);
        }

        private async Task<SharedKernel.Common.Models.Result<int>> DeliverAsync(ProcessInboundTransactionsCommand command)
        {
            using var messaging = _fixture.CreateMessagingContext();
            using var context = _fixture.CreateContext(messaging);
            using var bankLinks = _fixture.CreateBankLinksContext();
            var handler = IngestionHandlerFactory.Create(context, messaging, new BankLinksReadApi(bankLinks));
            return await handler.Handle(command, CancellationToken.None);
        }

        private async Task<(CustomerId CustomerId, AccountId AccountId)> LinkHolderAsync(string name, string externalAccountId)
        {
            using var customers = _fixture.CreateCustomersContext();
            var customer = Customer.Create(CustomerId.Create(), $"{Guid.NewGuid()}@example.com", name);
            var account = Account.Create(customer.Id, $"acc-{Guid.NewGuid():N}", $"{name}'s view of the joint account", AccountType.Checking, "ZAR");
            customers.Customers.Add(customer);
            customers.Accounts.Add(account);
            await customers.SaveChangesAsync();

            using var bankLinks = _fixture.CreateBankLinksContext();
            var link = BankLink.Create(customer.Id, Institution.FNB);
            link.Activate(account.Id, externalAccountId, "enc-a", "enc-r", DateTime.UtcNow.AddHours(1));
            bankLinks.BankLinks.Add(link);
            await bankLinks.SaveChangesAsync();

            return (customer.Id, account.Id);
        }
    }
}