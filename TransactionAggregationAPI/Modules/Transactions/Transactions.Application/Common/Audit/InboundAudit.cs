using BuildingBlocks.Messaging.Outbox;
using BuildingBlocks.Messaging.Persistence;
using Modules.Audit.Contracts;
using System.Diagnostics;
using System.Text.Json;

namespace Modules.Transactions.Application.Common.Audit
{
    /// <summary>
    /// Queues audit records through the transactional outbox, so they commit in the same
    /// SaveChanges as the inbox/transaction write they describe (and roll back with it).
    /// The outbox dispatcher hands them to the Audit module via IAuditTrail.
    /// </summary>
    public static class InboundAudit
    {
        public static OutboxMessage? Enqueue(IMessagingDbContext messaging, IReadOnlyList<AuditEventRecord> events)
        {
            if (events.Count == 0)
                return null;

            var message = OutboxMessage.Create(
                AuditOutbox.MessageType, JsonSerializer.Serialize(new AuditOutboxPayload(events)));
            messaging.OutboxMessages.Add(message);
            return message;
        }

        public static string? CurrentTraceId =>
            Activity.Current is { } activity ? activity.TraceId.ToString() : null;
    }
}