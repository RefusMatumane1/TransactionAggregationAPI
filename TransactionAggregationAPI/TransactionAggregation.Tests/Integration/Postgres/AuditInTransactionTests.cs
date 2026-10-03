using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Modules.Audit.Contracts;
using Modules.Audit.Domain;
using Modules.Transactions.Application.Common.DTOs;
using Modules.Transactions.Application.Features.Transactions.Commands.ReceiveBankTransactions;
using Xunit;

namespace TransactionAggregation.Tests.Integration.Postgres
{
    [Collection(PostgresCollection.Name)]
    public class AuditInTransactionTests(PostgresContainerFixture fixture)
    {
        private static ReceiveBankTransactionsCommand Command(string source) =>
            new(source, "ext-acc-1", null,
            [
                new ExternalTransactionDTO
                {
                    Id = "txn-1", Amount = -10m, Currency = "ZAR", Description = "Receipt audit",
                    Category = string.Empty, Date = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                }
            ], "delivery-1");

        [Fact]
        public async Task Receive_StoresTheReceivedAuditRowInTheSameCommitAsTheInboxRow()
        {
            var source = $"receipt-audit-{Guid.NewGuid():N}";
            using var messaging = fixture.CreateMessagingContext();
            var handler = new ReceiveBankTransactionsCommandHandler(
                messaging, fixture.CreateRetryingContext(messaging), NullLogger<ReceiveBankTransactionsCommandHandler>.Instance);

            var result = await handler.Handle(Command(source), CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            using var audit = fixture.CreateAuditContext();
            var received = await audit.AuditEvents.SingleAsync(e => e.SourceName == source);
            received.EventType.Should().Be(AuditEventTypes.InboundReceived);
            received.InboxMessageId.Should().Be(result.Value.InboxMessageId);

            using var verify = fixture.CreateMessagingContext();
            var queued = await verify.OutboxMessages.ToListAsync();
            queued.Should().NotContain(m => m.Payload.Contains(source),
                "the receipt is audited directly in its own transaction, not queued for the outbox dispatcher");
        }

        [Fact]
        public async Task Receive_AuditWriteFails_InboxRowIsRolledBack()
        {
            var source = $"receipt-rollback-{Guid.NewGuid():N}";
            var clashingId = Guid.NewGuid();
            using (var seed = fixture.CreateAuditContext())
            {
                seed.AuditEvents.Add(AuditEvent.Create(clashingId, AuditEventTypes.InboundReceived, DateTime.UtcNow, AuditChannels.Webhook, "seed"));
                await seed.SaveChangesAsync();
            }

            using var messaging = fixture.CreateMessagingContext();
            using var context = fixture.CreateRetryingContext(messaging);
            messaging.InboxMessages.Add(BuildingBlocks.Messaging.Inbox.InboxMessage.Create(source, "{}", "delivery-1", AuditChannels.Webhook));
            context.StageAudit([new AuditEventRecord(clashingId, AuditEventTypes.InboundReceived, DateTime.UtcNow, AuditChannels.Webhook, source)]);

            var save = () => context.SaveChangesAsync();

            await save.Should().ThrowAsync<DbUpdateException>();
            using var verify = fixture.CreateMessagingContext();
            (await verify.InboxMessages.CountAsync(m => m.SourceName == source))
                .Should().Be(0, "an inbox row must never be committed without its audit row");
        }
    }
}