using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Modules.Transactions.Application.Common.DTOs;
using Modules.Transactions.Application.Features.Transactions.Commands.ReceiveBankTransactions;
using Xunit;

namespace TransactionAggregation.Tests.Integration.Postgres
{
    /// <summary>
    /// The in-memory provider can't enforce IX_InboxMessages_SourceName_IdempotencyKey, so
    /// only a real Postgres run proves two concurrent deliveries of the same key (two API
    /// replicas receiving one webhook retry, or a Kafka rebalance redelivering a record)
    /// leave exactly one inbox row, with the loser reporting a duplicate rather than failing.
    /// </summary>
    [Collection(PostgresCollection.Name)]
    public class InboxIdempotencyTests
    {
        private readonly PostgresContainerFixture _fixture;

        public InboxIdempotencyTests(PostgresContainerFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task Handle_SameIdempotencyKeyReceivedConcurrently_StoresExactlyOneInboxMessage()
        {
            var sourceName = $"inbox-race-{Guid.NewGuid():N}";
            var dto = new ExternalTransactionDTO
            {
                Id = "txn-1",
                Amount = -10m,
                Currency = "ZAR",
                Description = "Inbox race",
                Category = string.Empty,
                Date = DateTime.UtcNow
            };
            var command = new ReceiveBankTransactionsCommand(sourceName, "ext-acc-1", [dto], "delivery-1");

            async Task<InboxReceipt> ReceiveAsync()
            {
                using var messaging = _fixture.CreateMessagingContext();
                var handler = new ReceiveBankTransactionsCommandHandler(
                    messaging, NullLogger<ReceiveBankTransactionsCommandHandler>.Instance);
                var result = await handler.Handle(command, CancellationToken.None);
                result.IsSuccess.Should().BeTrue();
                return result.Value;
            }

            var receipts = await Task.WhenAll(ReceiveAsync(), ReceiveAsync(), ReceiveAsync());

            receipts.Select(r => r.InboxMessageId).Distinct().Should().ContainSingle();
            receipts.Count(r => !r.IsDuplicate).Should().Be(1);

            using var verify = _fixture.CreateMessagingContext();
            (await verify.InboxMessages.CountAsync(m => m.SourceName == sourceName)).Should().Be(1);
        }
    }
}