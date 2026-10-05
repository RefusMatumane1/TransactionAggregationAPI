using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Modules.Transactions.Application.Common.Interfaces;
using Modules.Transactions.Application.Features.Transactions.Commands.ProcessInboundTransactions;
using Modules.Transactions.Application.Features.Transactions.Commands.ReceiveBankTransactions;
using Modules.Transactions.Domain.Enums;
using NSubstitute;
using System.Text.Json;
using TransactionAggregation.Tests.Helpers;
using Xunit;

namespace TransactionAggregation.Tests.Integration.Postgres
{
    [Collection(PostgresCollection.Name)]
    public class IngestionDateNormalizationTests
    {
        private readonly PostgresContainerFixture _fixture;

        public IngestionDateNormalizationTests(PostgresContainerFixture fixture)
        {
            _fixture = fixture;
        }

        [Theory]
        [InlineData("2026-09-10T12:00:00", "2026-09-10T10:00:00Z")]
        [InlineData("2026-09-10T12:00:00+02:00", "2026-09-10T10:00:00Z")]
        [InlineData("2026-09-10T10:00:00Z", "2026-09-10T10:00:00Z")]
        public async Task Handle_BankDateInAnyTimestampForm_IsStoredAsTheCorrectUtcInstant(string bankDate, string expectedUtc)
        {
            var externalAccountId = $"ext-acc-date-{Guid.NewGuid():N}";

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
            var handler = IngestionHandlerFactory.Create(context, messaging);

            var result = await handler.Handle(
                new ProcessInboundTransactionsCommand("date-test-source", message.ExternalAccountId, message.Institution, message.ToExternalTransactionDtos()),
                CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            result.Value.Should().Be(1);

            using var verify = _fixture.CreateContext();
            var stored = await verify.Transactions.SingleAsync(t => t.ExternalAccountId == externalAccountId);
            stored.Date.Should().Be(DateTime.Parse(expectedUtc, null, System.Globalization.DateTimeStyles.AdjustToUniversal));
        }
    }

    internal static class IngestionHandlerFactory
    {
        public static ProcessInboundTransactionsCommandHandler Create(
            Modules.Transactions.Infrastructure.Persistence.TransactionsDbContext context,
            BuildingBlocks.Messaging.Persistence.IMessagingDbContext messaging)
        {
            var categorization = Substitute.For<ITransactionCategorizationService>();
            categorization.Categorize(Arg.Any<string>(), Arg.Any<decimal>(), Arg.Any<TransactionCategory?>())
                .Returns(TransactionCategory.Uncategorized);

            return new ProcessInboundTransactionsCommandHandler(
                context, messaging, TestNormalizers.Shipped(), categorization,
                NullLogger<ProcessInboundTransactionsCommandHandler>.Instance);
        }
    }
}