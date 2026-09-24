using Microsoft.EntityFrameworkCore;
using Modules.Audit.Application.DTOs;
using Modules.Audit.Application.Persistence;
using Modules.Audit.Contracts;
using SharedKernel.Abstractions;
using SharedKernel.Common.Models;

namespace Modules.Audit.Application.Features.GetTransactionLineage
{
    public sealed record GetTransactionLineageQuery(Guid TransactionId) : IQuery<TransactionLineageDto>;

    internal sealed class GetTransactionLineageQueryHandler(IAuditDbContext context)
        : IQueryHandler<GetTransactionLineageQuery, TransactionLineageDto>
    {
        public async Task<Result<TransactionLineageDto>> Handle(GetTransactionLineageQuery request, CancellationToken cancellationToken)
        {
            var ingested = await context.AuditEvents
                .AsNoTracking()
                .Where(e => e.TransactionId == request.TransactionId && e.EventType == AuditEventTypes.TransactionIngested)
                .OrderBy(e => e.OccurredAt)
                .FirstOrDefaultAsync(cancellationToken);

            // Seeded / pre-audit transactions have no ingestion record — that's "no lineage", not an error in the data.
            if (ingested is null)
                return Result.Failure<TransactionLineageDto>(Error.NotFound("TransactionLineage", request.TransactionId));

            // The ingesting delivery's own events (receipt, retries, processing) — not every
            // per-transaction event of a 500-transaction batch, which would bury the one that
            // matters — plus anything later about this transaction itself, e.g. the settlement
            // that arrived in a different delivery when the bank posted it.
            var inboxMessageId = ingested.InboxMessageId;
            var events = await context.AuditEvents
                .AsNoTracking()
                .Where(e => e.TransactionId == request.TransactionId
                            || (inboxMessageId != null && e.InboxMessageId == inboxMessageId && e.TransactionId == null))
                .OrderBy(e => e.OccurredAt)
                .ThenBy(e => e.RecordedAt)
                .ToListAsync(cancellationToken);

            var received = events.FirstOrDefault(e => e.EventType == AuditEventTypes.InboundReceived);

            return Result.Success(new TransactionLineageDto(
                TransactionId: request.TransactionId,
                ExternalTransactionId: ingested.ExternalTransactionId,
                Channel: received?.Channel ?? ingested.Channel,
                SourceName: ingested.SourceName,
                InboxMessageId: ingested.InboxMessageId,
                ReceivedAt: received?.OccurredAt,
                IngestedAt: ingested.OccurredAt,
                Events: events.Select(AuditEventDto.From).ToList()));
        }
    }
}