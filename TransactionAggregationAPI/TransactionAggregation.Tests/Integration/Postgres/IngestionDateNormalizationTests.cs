using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Modules.BankLinks.Application.Contracts;
using Modules.BankLinks.Domain;
using Modules.BankLinks.Domain.ValueObjects;
using Modules.Customers.Domain;
using Modules.Customers.Domain.ValueObjects;
using Modules.Transactions.Application.Common.Interfaces;
using Modules.Transactions.Application.Features.Transactions.Commands.ProcessInboundTransactions;
using Modules.Transactions.Application.Features.Transactions.Commands.ReceiveBankTransactions;
using Modules.Transactions.Domain.Entities;
using Modules.Transactions.Domain.Enums;
using NSubstitute;
using SharedKernel.Common.ValueObjects;
using System.Text.Json;
using TransactionAggregation.Tests.Helpers;
using Xunit;

namespace TransactionAggregation.Tests.Integration.Postgres
{
    /// <summary>
    /// Banks don't all send UTC timestamps. JSON deserialization turns a date with no offset
    /// into DateTimeKind.Unspecified and one with a "+02:00" offset into DateTimeKind.Local —
    /// and the Date column is "timestamp with time zone", which Npgsql only accepts as UTC.
    /// Only a real Postgres run shows whether such a delivery can actually be stored.
    /// </summary>
    [Collection(PostgresCollection.Name)]
    public class IngestionDateNormalizationTests
    {
        private readonly PostgresContainerFixture _fixture;

        public IngestionDateNormalizationTests(PostgresContainerFixture fixture)
        {
            _fixture = fixture;
        }

        [Theory]
        [InlineData("2026-09-10T12:00:00", "2026-09-10T10:00:00Z")]        // no offset: bank-local (SAST, UTC+2)
        [InlineData("2026-09-10T12:00:00+02:00", "2026-09-10T10:00:00Z")]  // explicit offset
        [InlineData("2026-09-10T10:00:00Z", "2026-09-10T10:00:00Z")]       // already UTC
        public async Task Handle_BankDateInAnyTimestampForm_IsStoredAsTheCorrectUtcInstant(string bankDate, string expectedUtc)
        {
            var (externalAccountId, customerId) = await SeedLinkedAccountAsync(Institution.FNB);

            var payload = $$"""
                {
                  "externalAccountId": "{{externalAccountId}}",
                  "transactions": [
                    { "id": "date-{{Guid.NewGuid():N}}", "amount": -42.50, "currency": "ZAR",
                      "description": "Checkers Sandton", "date": "{{bankDate}}" }
                  ]
                }
                """;
            var message = JsonSerializer.Deserialize<BankTransactionsMessage>(payload, JsonSerializerOptions.Web)!;

            using var messaging = _fixture.CreateMessagingContext();
            using var context = _fixture.CreateContext(messaging);
            using var bankLinks = _fixture.CreateBankLinksContext();
            var handler = IngestionHandlerFactory.Create(context, messaging, new BankLinksReadApi(bankLinks));

            var result = await handler.Handle(
                new ProcessInboundTransactionsCommand("date-test-source", externalAccountId, message.ToExternalTransactionDtos()),
                CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            result.Value.Should().Be(1);

            using var verify = _fixture.CreateContext();
            var stored = await verify.Transactions.SingleAsync(t => t.CustomerId == customerId);
            stored.Date.Should().Be(DateTime.Parse(expectedUtc, null, System.Globalization.DateTimeStyles.AdjustToUniversal));
        }

        private async Task<(string ExternalAccountId, CustomerId CustomerId)> SeedLinkedAccountAsync(Institution institution)
        {
            using var customers = _fixture.CreateCustomersContext();
            var customer = Customer.Create(CustomerId.Create(), $"{Guid.NewGuid()}@example.com", "Date Test User");
            var account = Account.Create(customer.Id, $"acc-{Guid.NewGuid():N}", "Date Test Account", AccountType.Checking, "ZAR");
            customers.Customers.Add(customer);
            customers.Accounts.Add(account);
            await customers.SaveChangesAsync();

            using var bankLinks = _fixture.CreateBankLinksContext();
            var externalAccountId = $"ext-acc-date-{Guid.NewGuid():N}";
            var link = BankLink.Create(customer.Id, institution);
            link.Activate(account.Id, externalAccountId, "enc-a", "enc-r", DateTime.UtcNow.AddHours(1));
            bankLinks.BankLinks.Add(link);
            await bankLinks.SaveChangesAsync();

            return (externalAccountId, customer.Id);
        }
    }

    /// <summary>Builds the ingestion handler the way these Postgres tests need it, in one place.</summary>
    internal static class IngestionHandlerFactory
    {
        public static ProcessInboundTransactionsCommandHandler Create(
            Modules.Transactions.Infrastructure.Persistence.TransactionsDbContext context,
            BuildingBlocks.Messaging.Persistence.IMessagingDbContext messaging,
            BankLinksReadApi bankLinksReadApi)
        {
            var categorization = Substitute.For<ITransactionCategorizationService>();
            categorization.CategorizeTransactionAsync(Arg.Any<Transaction>(), Arg.Any<TransactionCategory?>(), Arg.Any<CancellationToken>())
                .Returns(TransactionCategory.Uncategorized);

            // The shipped rules, not neutral ones: the bank-local time zone comes from normalization-rules.json.
            return new ProcessInboundTransactionsCommandHandler(
                context, messaging, bankLinksReadApi, TestInstitutions.AllowAllDirectory(), TestNormalizers.Shipped(), categorization,
                NullLogger<ProcessInboundTransactionsCommandHandler>.Instance);
        }
    }
}