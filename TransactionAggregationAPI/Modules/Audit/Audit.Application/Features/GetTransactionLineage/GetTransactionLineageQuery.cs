using BuildingBlocks.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Modules.Audit.Application.DTOs;
using Modules.Audit.Application.Persistence;
using Modules.Audit.Contracts;
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

            if (ingested is null)
                return Result.Failure<TransactionLineageDto>(Error.NotFound("TransactionLineage", request.TransactionId));

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