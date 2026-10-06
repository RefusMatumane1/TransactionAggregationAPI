using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Application.Logging;
using BuildingBlocks.Messaging.Observability;
using BuildingBlocks.Messaging.Outbox;
using BuildingBlocks.Messaging.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Modules.Audit.Contracts;
using Modules.Transactions.Application.Common.Audit;
using Modules.Transactions.Application.Common.DTOs;
using Modules.Transactions.Application.Common.Errors;
using Modules.Transactions.Application.Common.Inbox;
using Modules.Transactions.Application.Common.Interfaces;
using Modules.Transactions.Application.Common.Outbox;
using Modules.Transactions.Contracts.IntegrationEvents;
using Modules.Transactions.Domain.Common.ValueObjects;
using Modules.Transactions.Domain.Entities;
using Modules.Transactions.Domain.Services;
using SharedKernel.Common.Models;
using System.Globalization;
using System.Text.Json;

namespace Modules.Transactions.Application.Features.Transactions.Commands.ProcessInboundTransactions
{
    // The unique key (institution, account, bank transaction id) over booked rows is the guarantee;
    // the existence check only spares the database a predictable conflict.
    internal sealed class ProcessInboundTransactionsCommandHandler(
        ITransactionsDbContext context,
        IMessagingDbContext messaging,
        ITransactionNormalizer normalizer,
        ITransactionCategorizationService categorizationService,
        ILogger<ProcessInboundTransactionsCommandHandler> logger)
        : ICommandHandler<ProcessInboundTransactionsCommand, int>
    {
        private const int MaxConflictAttempts = 3;

        private const string RepeatedInDelivery = "Repeated within the same delivery";
        private const string AlreadyRecorded = "Already recorded for this account";
        private const string NotYetPosted = "Pending at the bank: only posted transactions are recorded";

        private sealed record Skipped(string ExternalId, string Reason);

        private sealed record Outcome(List<Transaction> Recorded, List<Skipped> Duplicates, List<Skipped> Pending)
        {
            public bool IsEmpty => Recorded.Count == 0 && Duplicates.Count == 0 && Pending.Count == 0;
        }

        public async Task<Result<int>> Handle(ProcessInboundTransactionsCommand request, CancellationToken cancellationToken)
        {
            if (!InboundBank.Matches(request.SourceName, request.Institution))
            {
                logger.LogWarning(
                    "Delivery from {SourceName} names institution {Institution} (account {AccountRef}) — refused",
                    request.SourceName, request.Institution, LogRedaction.Account(request.ExternalAccountId));
                return Result.Failure<int>(TransactionErrors.SourceNotAuthorizedForInstitution(request.SourceName, request.Institution!));
            }

            for (var attempt = 1; ; attempt++)
            {
                var outcome = await StageAsync(request, cancellationToken);
                if (outcome.IsEmpty)
                    return Result.Success(0);

                EnqueueMessages(request, outcome);

                try
                {
                    await context.SaveChangesAsync(cancellationToken);
                }
                catch (Exception ex)
                {
                    context.DiscardPendingChanges();

                    if (ex is not DbUpdateException dbEx || !dbEx.IsUniqueViolation())
                        throw;

                    if (attempt >= MaxConflictAttempts)
                    {
                        logger.LogWarning(
                            "Delivery from {SourceName} for account {AccountRef} still conflicting after {Attempts} attempts — deferring to inbox retry",
                            request.SourceName, LogRedaction.Account(request.ExternalAccountId), attempt);
                        return Result.Failure<int>(Error.Conflict(
                            $"Concurrent ingestion kept conflicting after {attempt} attempts"));
                    }

                    logger.LogInformation(
                        "Delivery from {SourceName} for account {AccountRef} raced a concurrent insert (attempt {Attempt}) — re-checking for duplicates",
                        request.SourceName, LogRedaction.Account(request.ExternalAccountId), attempt);
                    continue;
                }

                LogCommitted(request, outcome);
                return Result.Success(outcome.Recorded.Count);
            }
        }

        private async Task<Outcome> StageAsync(ProcessInboundTransactionsCommand request, CancellationToken cancellationToken)
        {
            var normalized = request.Transactions
                .Select(raw => normalizer.Normalize(raw, request.SourceName))
                .ToList();

            var pending = normalized
                .Where(t => t.IsPending)
                .Select(t => new Skipped(t.ExternalId, NotYetPosted))
                .ToList();

            var duplicates = new List<Skipped>();
            var posted = new Dictionary<string, NormalizedTransaction>(StringComparer.Ordinal);
            foreach (var dto in normalized.Where(t => !t.IsPending))
            {
                if (!posted.TryAdd(dto.ExternalId, dto))
                    duplicates.Add(new Skipped(dto.ExternalId, RepeatedInDelivery));
            }

            var ids = posted.Keys.ToList();
            var alreadyRecorded = await context.Transactions
                .Where(LedgerEntries.IsEntry)
                .Where(t => t.Source.Name == request.SourceName
                            && t.ExternalAccountId == request.ExternalAccountId
                            && ids.Contains(t.Source.ExternalId))
                .Select(t => t.Source.ExternalId)
                .ToListAsync(cancellationToken);

            var recorded = new List<Transaction>();
            foreach (var dto in posted.Values)
            {
                if (alreadyRecorded.Contains(dto.ExternalId, StringComparer.Ordinal))
                {
                    duplicates.Add(new Skipped(dto.ExternalId, AlreadyRecorded));
                    continue;
                }

                recorded.Add(Transaction.Record(
                    request.ExternalAccountId,
                    Money.Create(dto.Amount, dto.Currency),
                    dto.Description,
                    categorizationService.Categorize(dto.Description, dto.Amount, dto.BankCategory),
                    TransactionSource.Create(request.SourceName, dto.ExternalId),
                    dto.DateUtc,
                    Provenance(dto)));
            }

            context.Transactions.AddRange(recorded);
            return new Outcome(recorded, duplicates, pending);
        }

        private static Dictionary<string, string> Provenance(NormalizedTransaction dto)
        {
            var metadata = new Dictionary<string, string>();
            if (dto.OriginalDescription is not null)
                metadata[BankProvenanceMetadata.Description] = dto.OriginalDescription;
            if (dto.OriginalCategory is not null)
                metadata[BankProvenanceMetadata.Category] = dto.OriginalCategory;
            return metadata;
        }

        private void EnqueueMessages(ProcessInboundTransactionsCommand request, Outcome outcome)
        {
            foreach (var transaction in outcome.Recorded)
            {
                var message = OutboxMessage.Create(
                    OutboxMessageTypes.TransactionRecorded,
                    eventId => JsonSerializer.Serialize(new TransactionRecorded(
                        EventId: eventId,
                        TransactionId: transaction.Id.Value,
                        Institution: transaction.Source.Name,
                        ExternalAccountId: transaction.ExternalAccountId,
                        ExternalTransactionId: transaction.Source.ExternalId,
                        Amount: transaction.Amount.Amount,
                        Currency: transaction.Amount.Currency,
                        Description: transaction.Description,
                        Category: transaction.Category.ToString(),
                        BookedAt: transaction.Date,
                        RecordedAt: transaction.CreatedAt)),
                    TransactionRecorded.SchemaVersion);
                messaging.OutboxMessages.Add(message);
            }

            if (outcome.Duplicates.Count > 0)
            {
                var notification = new DuplicateInboundDetectedOutboxPayload(
                    Level: DuplicateMetrics.TransactionLevel,
                    SourceName: request.SourceName,
                    ExternalAccountId: request.ExternalAccountId,
                    DuplicateExternalIds: outcome.Duplicates.Select(d => d.ExternalId).ToList(),
                    InboxMessageId: request.InboxMessageId,
                    DetectedAt: DateTime.UtcNow);
                messaging.OutboxMessages.Add(OutboxMessage.Create(
                    OutboxMessageTypes.DuplicateInboundDetected, JsonSerializer.Serialize(notification)));
            }

            context.StageAudit(BuildAuditEvents(request, outcome));
        }

        private void LogCommitted(ProcessInboundTransactionsCommand request, Outcome outcome)
        {
            if (outcome.Duplicates.Count > 0)
            {
                DuplicateMetrics.InboundDuplicates
                    .WithLabels(request.SourceName, DuplicateMetrics.TransactionLevel)
                    .Inc(outcome.Duplicates.Count);
                logger.LogWarning(
                    "Skipped {DuplicateCount} duplicate transactions from bank {SourceName} for account {AccountRef}",
                    outcome.Duplicates.Count, request.SourceName, LogRedaction.Account(request.ExternalAccountId));
            }

            logger.LogInformation(
                "Recorded {RecordedCount} transactions from bank {SourceName} for account {AccountRef} ({PendingCount} pending notices not recorded)",
                outcome.Recorded.Count, request.SourceName, LogRedaction.Account(request.ExternalAccountId), outcome.Pending.Count);
        }

        private static List<AuditEventRecord> BuildAuditEvents(ProcessInboundTransactionsCommand request, Outcome outcome)
        {
            var channel = request.Channel ?? AuditChannels.Unknown;
            var now = DateTime.UtcNow;
            var traceId = InboundAudit.CurrentTraceId;

            AuditEventRecord Event(string type, Guid? transactionId = null, string? externalId = null,
                string? detail = null, Dictionary<string, string>? metadata = null) => new(
                EventId: Guid.NewGuid(),
                EventType: type,
                OccurredAt: now,
                Channel: channel,
                SourceName: request.SourceName,
                ExternalAccountId: request.ExternalAccountId,
                InboxMessageId: request.InboxMessageId,
                TransactionId: transactionId,
                ExternalTransactionId: externalId,
                Detail: detail,
                Metadata: metadata,
                TraceId: traceId);

            var events = new List<AuditEventRecord>(
                outcome.Recorded.Count + outcome.Duplicates.Count + outcome.Pending.Count + 1);

            events.AddRange(outcome.Recorded.Select(t => Event(
                AuditEventTypes.TransactionIngested,
                t.Id.Value,
                t.Source.ExternalId,
                detail: "Recorded in the ledger",
                metadata: new()
                {
                    ["institution"] = request.SourceName,
                    ["amount"] = t.Amount.Amount.ToString(CultureInfo.InvariantCulture),
                    ["currency"] = t.Amount.Currency,
                    ["category"] = t.Category.ToString()
                })));

            events.AddRange(outcome.Duplicates.Select(d => Event(
                AuditEventTypes.TransactionDuplicateSkipped, externalId: d.ExternalId, detail: d.Reason)));

            events.AddRange(outcome.Pending.Select(p => Event(
                AuditEventTypes.TransactionPendingSkipped, externalId: p.ExternalId, detail: p.Reason)));

            events.Add(Event(
                AuditEventTypes.InboundProcessed,
                detail: $"{outcome.Recorded.Count} recorded, {outcome.Duplicates.Count} duplicate(s) skipped, {outcome.Pending.Count} pending notice(s) not recorded",
                metadata: new()
                {
                    ["recordedCount"] = outcome.Recorded.Count.ToString(CultureInfo.InvariantCulture),
                    ["duplicateCount"] = outcome.Duplicates.Count.ToString(CultureInfo.InvariantCulture),
                    ["pendingCount"] = outcome.Pending.Count.ToString(CultureInfo.InvariantCulture),
                    ["institution"] = request.SourceName
                }));

            return events;
        }
    }
}