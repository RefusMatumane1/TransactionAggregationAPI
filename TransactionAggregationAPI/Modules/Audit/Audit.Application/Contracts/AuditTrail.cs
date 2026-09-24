using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Modules.Audit.Application.Persistence;
using Modules.Audit.Contracts;
using Modules.Audit.Domain;

namespace Modules.Audit.Application.Contracts
{
    internal sealed class AuditTrail(IAuditDbContext context, ILogger<AuditTrail> logger) : IAuditTrail
    {
        /// <summary>
        /// A conflict means a concurrent writer (e.g. two dispatcher replicas that both
        /// reclaimed the same stale outbox message) stored some of these ids first; a
        /// re-check turns those into no-ops.
        /// </summary>
        private const int MaxConflictAttempts = 3;

        public async Task RecordAsync(IReadOnlyCollection<AuditEventRecord> events, CancellationToken cancellationToken = default)
        {
            var distinct = events
                .GroupBy(e => e.EventId)
                .Select(g => g.First())
                .ToList();
            if (distinct.Count == 0)
                return;

            var ids = distinct.Select(e => e.EventId).ToList();

            for (var attempt = 1; ; attempt++)
            {
                var alreadyRecorded = await context.AuditEvents
                    .Where(e => ids.Contains(e.Id))
                    .Select(e => e.Id)
                    .ToListAsync(cancellationToken);

                var toAdd = distinct
                    .Where(e => !alreadyRecorded.Contains(e.EventId))
                    .Select(ToEntity)
                    .ToList();
                if (toAdd.Count == 0)
                    return;

                context.AuditEvents.AddRange(toAdd);

                try
                {
                    await context.SaveChangesAsync(cancellationToken);
                    return;
                }
                catch (DbUpdateException ex) when (attempt < MaxConflictAttempts)
                {
                    foreach (var entity in toAdd)
                        context.AuditEvents.Entry(entity).State = EntityState.Detached;

                    logger.LogInformation(ex,
                        "Audit write of {Count} events conflicted (attempt {Attempt}) — re-checking for already-recorded ids",
                        toAdd.Count, attempt);
                }
            }
        }

        private static AuditEvent ToEntity(AuditEventRecord e) => AuditEvent.Create(
            e.EventId,
            e.EventType,
            e.OccurredAt,
            e.Channel,
            e.SourceName,
            e.ExternalAccountId,
            e.InboxMessageId,
            e.IdempotencyKey,
            e.CustomerId,
            e.TransactionId,
            e.ExternalTransactionId,
            e.Detail,
            e.Metadata,
            e.TraceId);
    }
}