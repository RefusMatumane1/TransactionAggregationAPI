using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Modules.Audit.Application.Persistence;
using Modules.Audit.Contracts;
using Modules.Audit.Domain;
using System.Data.Common;

namespace Modules.Audit.Application.Contracts
{
    internal sealed class AuditTrail(IAuditDbContext context, ILogger<AuditTrail> logger) : IAuditTrail
    {
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

        public async Task RecordWithinAsync(
            IReadOnlyCollection<AuditEventRecord> events, DbTransaction transaction, CancellationToken cancellationToken = default)
        {
            var toAdd = events
                .GroupBy(e => e.EventId)
                .Select(g => ToEntity(g.First()))
                .ToList();
            if (toAdd.Count == 0)
                return;

            // No conflict retry: a failed statement aborts the caller's transaction, so the caller retries the whole unit.
            await context.Database.UseTransactionAsync(transaction, cancellationToken);
            try
            {
                context.AuditEvents.AddRange(toAdd);
                await context.SaveChangesAsync(cancellationToken);
            }
            finally
            {
                foreach (var entity in toAdd)
                    context.AuditEvents.Entry(entity).State = EntityState.Detached;
                await context.Database.UseTransactionAsync(null, CancellationToken.None);
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
            e.TransactionId,
            e.ExternalTransactionId,
            e.Detail,
            e.Metadata,
            e.TraceId,
            e.Actor);
    }
}