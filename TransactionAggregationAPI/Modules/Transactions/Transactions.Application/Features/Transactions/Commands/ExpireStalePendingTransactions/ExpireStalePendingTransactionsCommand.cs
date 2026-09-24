using BuildingBlocks.Messaging.Outbox;
using BuildingBlocks.Messaging.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Modules.Audit.Contracts;
using Modules.Transactions.Application.Common.Audit;
using Modules.Transactions.Application.Common.Interfaces;
using Modules.Transactions.Application.Common.Outbox;
using Modules.Transactions.Domain.Entities;
using Modules.Transactions.Domain.Enums;
using SharedKernel.Abstractions;
using SharedKernel.Common.Models;
using System.Globalization;
using System.Text.Json;

namespace Modules.Transactions.Application.Features.Transactions.Commands.ExpireStalePendingTransactions
{
    /// <summary>
    /// Expires up to <paramref name="BatchSize"/> transactions that have been Pending since
    /// before <paramref name="Cutoff"/> (both their bank date and our receipt time), oldest first.
    /// Returns how many were expired; a full batch means there may be more.
    /// </summary>
    public sealed record ExpireStalePendingTransactionsCommand(DateTime Cutoff, int BatchSize) : ICommand<int>;

    internal sealed class ExpireStalePendingTransactionsCommandHandler(
        ITransactionsDbContext context,
        IMessagingDbContext messaging,
        ILogger<ExpireStalePendingTransactionsCommandHandler> logger)
        : ICommandHandler<ExpireStalePendingTransactionsCommand, int>
    {
        public const string SourceName = "pending-expiry-job";

        public async Task<Result<int>> Handle(ExpireStalePendingTransactionsCommand request, CancellationToken cancellationToken)
        {
            var cutoff = DateTime.SpecifyKind(request.Cutoff, DateTimeKind.Utc);

            // Date < cutoff AND CreatedAt < cutoff  ⇔  Transaction.PendingSince < cutoff
            // (spelled out because PendingSince is computed, not a column).
            var stale = await context.Transactions
                .Where(t => t.Status == TransactionStatus.Pending && t.Date < cutoff && t.CreatedAt < cutoff)
                .OrderBy(t => t.CreatedAt)
                .ThenBy(t => t.Id)
                .Take(request.BatchSize)
                .ToListAsync(cancellationToken);

            if (stale.Count == 0)
                return Result.Success(0);

            foreach (var transaction in stale)
                transaction.Expire();

            messaging.OutboxMessages.Add(OutboxMessage.Create(
                OutboxMessageTypes.TransactionsExpired,
                JsonSerializer.Serialize(new TransactionsExpiredOutboxPayload(
                    stale.Select(t => t.CustomerId.Value).Distinct().ToList(), stale.Count))));

            InboundAudit.Enqueue(messaging, stale.Select(t => BuildAuditEvent(t, cutoff)).ToList());

            try
            {
                // Commits the status changes, the cache-invalidation message and the audit
                // records together (TransactionsDbContext shares its transaction with messaging).
                await context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                // Something else changed one of these rows since we read it — most likely the
                // bank's posting settled it, or another replica's job got there first. Nothing
                // from this batch is committed; whatever is still Pending is picked up next run.
                context.DiscardPendingChanges();
                logger.LogInformation(ex,
                    "Pending-expiry batch of {Count} lost a race with a concurrent update — will retry next run", stale.Count);
                return Result.Failure<int>(Error.Conflict("A transaction in the expiry batch was changed concurrently"));
            }

            logger.LogInformation(
                "Expired {Count} transactions pending since before {Cutoff:O}", stale.Count, cutoff);

            return Result.Success(stale.Count);
        }

        private static AuditEventRecord BuildAuditEvent(Transaction t, DateTime cutoff) => new(
            EventId: Guid.NewGuid(),
            EventType: AuditEventTypes.TransactionExpired,
            OccurredAt: DateTime.UtcNow,
            Channel: AuditChannels.System,
            SourceName: SourceName,
            CustomerId: t.CustomerId.Value,
            TransactionId: t.Id.Value,
            ExternalTransactionId: t.Source.ExternalId,
            Detail: $"No posting from the bank since {t.PendingSince:yyyy-MM-dd HH:mm} UTC — expired",
            Metadata: new Dictionary<string, string>
            {
                ["amount"] = t.Amount.Amount.ToString(CultureInfo.InvariantCulture),
                ["currency"] = t.Amount.Currency,
                ["institution"] = t.Source.Name,
                ["pendingSince"] = t.PendingSince.ToString("O", CultureInfo.InvariantCulture),
                ["cutoff"] = cutoff.ToString("O", CultureInfo.InvariantCulture)
            },
            TraceId: InboundAudit.CurrentTraceId);
    }
}