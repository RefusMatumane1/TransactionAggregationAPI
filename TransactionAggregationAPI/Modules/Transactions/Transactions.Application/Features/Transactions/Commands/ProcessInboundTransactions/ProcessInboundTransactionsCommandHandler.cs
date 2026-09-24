using BuildingBlocks.Messaging.Observability;
using BuildingBlocks.Messaging.Outbox;
using BuildingBlocks.Messaging.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Modules.Audit.Contracts;
using Modules.BankLinks.Contracts;
using Modules.Transactions.Application.Common.Audit;
using Modules.Transactions.Application.Common.DTOs;
using Modules.Transactions.Application.Common.Errors;
using Modules.Transactions.Application.Common.Interfaces;
using Modules.Transactions.Application.Common.Outbox;
using Modules.Transactions.Application.Services;
using Modules.Transactions.Domain.Common.ValueObjects;
using Modules.Transactions.Domain.Entities;
using Modules.Transactions.Domain.Enums;
using Modules.WebhookSources.Contracts;
using SharedKernel.Abstractions;
using SharedKernel.Common.Interfaces;
using SharedKernel.Common.Models;
using SharedKernel.Common.ValueObjects;
using System.Globalization;
using System.Text.Json;

namespace Modules.Transactions.Application.Features.Transactions.Commands.ProcessInboundTransactions
{
    /// <summary>
    /// Turns one inbox delivery into stored transactions. The stages, in order: resolve every
    /// customer linked to the bank account — a joint account has one link per holder, and each
    /// holder gets their own copy under their own account; then, per holder, normalize every
    /// transaction with that link's bank profile and, per external transaction id:
    /// <list type="bullet">
    /// <item>not stored yet → created: Settled if the bank says posted (or says nothing), Pending if pending;</item>
    /// <item>stored as Pending — or Expired by the pending-expiry job — and now posted → the existing row is
    /// settled with the bank's posted amount/date (a late posting overrides our expiry guess);</item>
    /// <item>anything else (same state again, or a stale "pending" for a settled/expired row) → duplicate, skipped.</item>
    /// </list>
    /// All holders' copies commit in one unit of work, so a retry never finds one holder done
    /// and another not.
    /// </summary>
    internal sealed class ProcessInboundTransactionsCommandHandler(
        ITransactionsDbContext context,
        IMessagingDbContext messaging,
        IBankLinksReadApi bankLinksReadApi,
        IWebhookSourceDirectory sourceDirectory,
        ITransactionNormalizer normalizer,
        ITransactionCategorizationService categorizationService,
        ILogger<ProcessInboundTransactionsCommandHandler> logger)
        : ICommandHandler<ProcessInboundTransactionsCommand, int>
    {
        /// <summary>
        /// Each conflict means a concurrent writer committed some of this batch's external IDs
        /// between our existence check and our insert; re-checking picks those up as
        /// duplicates (or settlements). Needing more than a few rounds would mean something
        /// other than a benign race is going on, so the message goes back to the inbox's retry/backoff.
        /// </summary>
        private const int MaxConflictAttempts = 3;

        private const string RepeatedInDelivery = "Repeated within the same delivery";
        private const string AlreadyStored = "Already stored for this customer";
        private const string StalePending = "Pending update for a transaction that is already settled";
        private const string PendingAfterExpiry = "Pending update for a transaction that has already expired — only a posting reinstates it";

        private sealed record Duplicate(string ExternalId, string Reason);

        private sealed record Settlement(
            Transaction Transaction, TransactionStatus PreviousStatus, decimal PreviousAmount, DateTime PreviousDate);

        /// <summary>One holder's share of a delivery, staged but not yet saved.</summary>
        private sealed record LinkOutcome(
            ActiveBankLinkInfo Link,
            List<Transaction> NewTransactions,
            List<Settlement> Settlements,
            List<Duplicate> Duplicates)
        {
            public bool HasChanges => NewTransactions.Count > 0 || Settlements.Count > 0;
            public bool IsEmpty => !HasChanges && Duplicates.Count == 0;
        }

        private static bool CanBeSettledByPosting(TransactionStatus status) =>
            status is TransactionStatus.Pending or TransactionStatus.Expired;

        public async Task<Result<int>> Handle(ProcessInboundTransactionsCommand request, CancellationToken cancellationToken)
        {
            var allLinks = await bankLinksReadApi.FindActiveLinksByExternalAccountIdAsync(
                request.ExternalAccountId, cancellationToken);

            if (allLinks.Count == 0)
                return Result.Failure<int>(Error.NotFound("BankLink", request.ExternalAccountId));

            // A source may only write into accounts held at institutions it's authorized for:
            // the external account id alone is sender-supplied, so without this any source's
            // key could place transactions in any customer's account that shares the id.
            var authorized = await sourceDirectory.GetAuthorizedInstitutionsAsync(request.SourceName, cancellationToken);
            var links = allLinks.Where(l => authorized.Contains(l.InstitutionName)).ToList();

            if (links.Count < allLinks.Count)
                logger.LogWarning(
                    "Source {SourceName} is not authorized for {RefusedCount} of {LinkCount} bank links on account {ExternalAccountId} (institutions {RefusedInstitutions}) — those links are skipped",
                    request.SourceName, allLinks.Count - links.Count, allLinks.Count, request.ExternalAccountId,
                    allLinks.Where(l => !authorized.Contains(l.InstitutionName)).Select(l => l.InstitutionName).Distinct());

            if (links.Count == 0)
                return Result.Failure<int>(TransactionErrors.SourceNotAuthorizedForAccount(request.SourceName, request.ExternalAccountId));

            for (var attempt = 1; ; attempt++)
            {
                var outcomes = new List<LinkOutcome>(links.Count);
                foreach (var link in links)
                    outcomes.Add(await StageForLinkAsync(request, link, cancellationToken));

                if (outcomes.All(o => o.IsEmpty))
                    return Result.Success(0);

                // Rebuilt on every attempt: a conflicting attempt's messages are discarded with
                // the rest of its pending changes, so only what actually committed is announced.
                foreach (var outcome in outcomes)
                    EnqueueMessages(request, outcome);

                try
                {
                    // TransactionsDbContext flushes the shared MessagingDbContext in the same
                    // transaction; with no transaction changes only the notifications are pending.
                    if (outcomes.Any(o => o.HasChanges))
                        await context.SaveChangesAsync(cancellationToken);
                    else
                        await messaging.SaveChangesAsync(cancellationToken);
                }
                catch (Exception ex)
                {
                    // Whatever this attempt left tracked must not leak into a retry, or into the
                    // inbox dispatcher's own SaveChangesAsync later in the same scope.
                    context.DiscardPendingChanges();

                    if (ex is not DbUpdateException dbEx || !dbEx.IsUniqueViolation())
                        throw;

                    if (attempt >= MaxConflictAttempts)
                    {
                        logger.LogWarning(
                            "Inbound batch from {SourceName} for account {ExternalAccountId} still conflicting after {Attempts} attempts — deferring to inbox retry",
                            request.SourceName, request.ExternalAccountId, attempt);
                        return Result.Failure<int>(Error.Conflict(
                            $"Concurrent ingestion kept conflicting after {attempt} attempts"));
                    }

                    logger.LogInformation(
                        "Inbound batch from {SourceName} for account {ExternalAccountId} raced a concurrent insert (attempt {Attempt}) — re-checking for duplicates",
                        request.SourceName, request.ExternalAccountId, attempt);
                    continue;
                }

                foreach (var outcome in outcomes)
                    LogCommitted(request, outcome);

                return Result.Success(outcomes.Sum(o => o.NewTransactions.Count));
            }
        }

        /// <summary>
        /// Stages one holder's share of the delivery — new rows added to the context, settled
        /// rows modified — without saving. Each holder is checked against their own stored
        /// transactions, so the same delivery can be new for one holder and a duplicate for another.
        /// </summary>
        private async Task<LinkOutcome> StageForLinkAsync(
            ProcessInboundTransactionsCommand request, ActiveBankLinkInfo link, CancellationToken cancellationToken)
        {
            var linkCustomerId = CustomerId.CreateFrom(link.CustomerId);
            var linkAccountId = AccountId.CreateFrom(link.AccountId);

            var normalized = request.Transactions
                .Select(raw => normalizer.Normalize(raw, link.InstitutionName))
                .ToList();

            // One entry per external id. The same id twice in one delivery would otherwise hit
            // the unique index against itself on every attempt; when a delivery carries both
            // the pending and the posted version, the posted one is the one that matters.
            var chosen = new Dictionary<string, NormalizedTransaction>();
            var repeatedInBatch = new List<string>();
            foreach (var dto in normalized)
            {
                if (!chosen.TryGetValue(dto.ExternalId, out var kept))
                {
                    chosen[dto.ExternalId] = dto;
                    continue;
                }

                repeatedInBatch.Add(dto.ExternalId);
                if (kept.IsPending && !dto.IsPending)
                    chosen[dto.ExternalId] = dto;
            }
            var ids = chosen.Keys.ToHashSet();

            // Scoped to the institution as well as the customer: external ids are only unique
            // per institution, so the same id from two banks is two different transactions.
            var existingStatus = await context.Transactions
                .Where(t => t.CustomerId == linkCustomerId
                            && t.Source.Name == link.InstitutionName
                            && ids.Contains(t.Source.ExternalId))
                .Select(t => new { t.Source.ExternalId, t.Status })
                .ToDictionaryAsync(t => t.ExternalId, t => t.Status, cancellationToken);

            var duplicates = repeatedInBatch.Select(id => new Duplicate(id, RepeatedInDelivery)).ToList();
            var toCreate = new List<NormalizedTransaction>();
            var toSettle = new List<NormalizedTransaction>();

            foreach (var dto in chosen.Values)
            {
                var incomingPending = dto.IsPending;

                if (!existingStatus.TryGetValue(dto.ExternalId, out var stored))
                    toCreate.Add(dto);
                else if (CanBeSettledByPosting(stored) && !incomingPending)
                    toSettle.Add(dto);
                else
                    duplicates.Add(new Duplicate(dto.ExternalId, (incomingPending, stored) switch
                    {
                        (true, TransactionStatus.Settled) => StalePending,
                        (true, TransactionStatus.Expired) => PendingAfterExpiry,
                        _ => AlreadyStored
                    }));
            }

            var newTransactions = new List<Transaction>();
            foreach (var dto in toCreate)
            {
                var transaction = Transaction.Create(
                    linkCustomerId,
                    Money.Create(dto.Amount, dto.Currency),
                    dto.Description,
                    TransactionCategory.Uncategorized,
                    TransactionSource.Create(link.InstitutionName, dto.ExternalId),
                    linkAccountId,
                    dto.DateUtc);

                if (!dto.IsPending)
                    transaction.Settle();

                // Cleaning never loses information: what the bank actually sent is kept.
                if (dto.OriginalDescription is not null)
                    transaction.AddMetadata(BankProvenanceMetadata.Description, dto.OriginalDescription);
                if (dto.OriginalCategory is not null)
                    transaction.AddMetadata(BankProvenanceMetadata.Category, dto.OriginalCategory);

                var category = await categorizationService.CategorizeTransactionAsync(
                    transaction, dto.BankCategory, cancellationToken);
                if (category != TransactionCategory.Uncategorized)
                    transaction.Categorize(category, isAuto: true);

                newTransactions.Add(transaction);
            }

            var settlements = await SettleAsync(linkCustomerId, link.InstitutionName, toSettle, duplicates, cancellationToken);

            await context.Transactions.AddRangeAsync(newTransactions, cancellationToken);

            return new LinkOutcome(link, newTransactions, settlements, duplicates);
        }

        /// <summary>
        /// Queues one holder's cache invalidation, duplicate notification and audit records in
        /// the same unit of work as their rows, so each exists if and only if the rows committed.
        /// </summary>
        private void EnqueueMessages(ProcessInboundTransactionsCommand request, LinkOutcome outcome)
        {
            var link = outcome.Link;

            // TransactionSynced drives cache invalidation for the customer's lists and
            // summaries — needed for a settlement too, since it moves money into the totals.
            foreach (var transaction in outcome.NewTransactions.Concat(outcome.Settlements.Select(s => s.Transaction)))
            {
                var payload = new TransactionSyncedOutboxPayload(
                    transaction.Id.Value, transaction.CustomerId.Value, link.InstitutionName);
                messaging.OutboxMessages.Add(OutboxMessage.Create(
                    OutboxMessageTypes.TransactionSynced, JsonSerializer.Serialize(payload)));
            }

            if (outcome.Duplicates.Count > 0)
            {
                var notification = new DuplicateInboundDetectedOutboxPayload(
                    Level: DuplicateMetrics.TransactionLevel,
                    SourceName: request.SourceName,
                    ExternalAccountId: request.ExternalAccountId,
                    DuplicateExternalIds: outcome.Duplicates.Select(d => d.ExternalId).ToList(),
                    InboxMessageId: request.InboxMessageId,
                    CustomerId: link.CustomerId,
                    DetectedAt: DateTime.UtcNow);
                messaging.OutboxMessages.Add(OutboxMessage.Create(
                    OutboxMessageTypes.DuplicateInboundDetected, JsonSerializer.Serialize(notification)));
            }

            InboundAudit.Enqueue(messaging,
                BuildAuditEvents(request, link, outcome.NewTransactions, outcome.Settlements, outcome.Duplicates));
        }

        private void LogCommitted(ProcessInboundTransactionsCommand request, LinkOutcome outcome)
        {
            var link = outcome.Link;

            if (outcome.Duplicates.Count > 0)
            {
                DuplicateMetrics.InboundDuplicates
                    .WithLabels(request.SourceName, DuplicateMetrics.TransactionLevel)
                    .Inc(outcome.Duplicates.Count);
                logger.LogWarning(
                    "Skipped {DuplicateCount} duplicate transactions from source {SourceName} for BankLink {BankLinkId}: {DuplicateExternalIds}",
                    outcome.Duplicates.Count, request.SourceName, link.BankLinkId, outcome.Duplicates.Select(d => d.ExternalId));
            }

            logger.LogInformation(
                "Ingested {Count} new and settled {SettledCount} pending transactions from source {SourceName} for BankLink {BankLinkId} ({Institution})",
                outcome.NewTransactions.Count, outcome.Settlements.Count, request.SourceName, link.BankLinkId, link.InstitutionName);
        }

        /// <summary>
        /// Loads (tracked) and settles the stored pending rows the bank has now posted. A row
        /// whose status changed since the existence check is no longer settleable and is
        /// reported as a duplicate instead.
        /// </summary>
        private async Task<List<Settlement>> SettleAsync(
            CustomerId customerId,
            string institutionName,
            IReadOnlyList<NormalizedTransaction> toSettle,
            List<Duplicate> duplicates,
            CancellationToken cancellationToken)
        {
            if (toSettle.Count == 0)
                return [];

            var settleIds = toSettle.Select(d => d.ExternalId).ToHashSet();
            var stored = await context.Transactions
                .Where(t => t.CustomerId == customerId
                            && t.Source.Name == institutionName
                            && settleIds.Contains(t.Source.ExternalId))
                .ToDictionaryAsync(t => t.Source.ExternalId, cancellationToken);

            var settlements = new List<Settlement>();
            foreach (var dto in toSettle)
            {
                if (!stored.TryGetValue(dto.ExternalId, out var transaction) || !CanBeSettledByPosting(transaction.Status))
                {
                    duplicates.Add(new Duplicate(dto.ExternalId, AlreadyStored));
                    continue;
                }

                var previousStatus = transaction.Status;
                var previousAmount = transaction.Amount.Amount;
                var previousDate = transaction.Date;
                transaction.Settle(Money.Create(dto.Amount, dto.Currency), dto.DateUtc);
                settlements.Add(new Settlement(transaction, previousStatus, previousAmount, previousDate));
            }

            return settlements;
        }

        private static List<AuditEventRecord> BuildAuditEvents(
            ProcessInboundTransactionsCommand request,
            ActiveBankLinkInfo link,
            IReadOnlyList<Transaction> newTransactions,
            IReadOnlyList<Settlement> settlements,
            IReadOnlyList<Duplicate> duplicates)
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
                CustomerId: link.CustomerId,
                TransactionId: transactionId,
                ExternalTransactionId: externalId,
                Detail: detail,
                Metadata: metadata,
                TraceId: traceId);

            string Money(decimal amount) => amount.ToString(CultureInfo.InvariantCulture);

            var events = new List<AuditEventRecord>(newTransactions.Count + settlements.Count + duplicates.Count + 1);

            events.AddRange(newTransactions.Select(t => Event(
                AuditEventTypes.TransactionIngested,
                t.Id.Value,
                t.Source.ExternalId,
                detail: $"Stored as {t.Status}",
                metadata: new()
                {
                    ["bankLinkId"] = link.BankLinkId.ToString(),
                    ["accountId"] = link.AccountId.ToString(),
                    ["institution"] = link.InstitutionName,
                    ["amount"] = Money(t.Amount.Amount),
                    ["currency"] = t.Amount.Currency,
                    ["status"] = t.Status.ToString()
                })));

            events.AddRange(settlements.Select(s =>
            {
                var t = s.Transaction;
                var metadata = new Dictionary<string, string>
                {
                    ["bankLinkId"] = link.BankLinkId.ToString(),
                    ["amount"] = Money(t.Amount.Amount),
                    ["currency"] = t.Amount.Currency,
                    ["previousStatus"] = s.PreviousStatus.ToString()
                };
                if (s.PreviousAmount != t.Amount.Amount)
                    metadata["pendingAmount"] = Money(s.PreviousAmount);
                if (s.PreviousDate != t.Date)
                    metadata["pendingDate"] = s.PreviousDate.ToString("O", CultureInfo.InvariantCulture);

                return Event(
                    AuditEventTypes.TransactionSettled,
                    t.Id.Value,
                    t.Source.ExternalId,
                    detail: (s.PreviousStatus == TransactionStatus.Expired
                                ? "Posted by the bank after it had expired — reinstated"
                                : "Posted by the bank")
                            + (s.PreviousAmount != t.Amount.Amount
                                ? $"; amount changed from {Money(s.PreviousAmount)} to {Money(t.Amount.Amount)}"
                                : string.Empty),
                    metadata: metadata);
            }));

            events.AddRange(duplicates.Select(d => Event(
                AuditEventTypes.TransactionDuplicateSkipped,
                externalId: d.ExternalId,
                detail: d.Reason)));

            events.Add(Event(
                AuditEventTypes.InboundProcessed,
                detail: $"{newTransactions.Count} ingested, {settlements.Count} settled, {duplicates.Count} duplicate(s) skipped",
                metadata: new()
                {
                    ["ingestedCount"] = newTransactions.Count.ToString(),
                    ["settledCount"] = settlements.Count.ToString(),
                    ["duplicateCount"] = duplicates.Count.ToString(),
                    ["bankLinkId"] = link.BankLinkId.ToString(),
                    ["institution"] = link.InstitutionName
                }));

            return events;
        }
    }
}