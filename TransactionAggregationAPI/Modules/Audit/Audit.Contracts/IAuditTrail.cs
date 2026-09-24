namespace Modules.Audit.Contracts
{
    /// <summary>
    /// The Audit module's write API. Records are durable once the call returns, and
    /// idempotent by <see cref="AuditEventRecord.EventId"/>.
    ///
    /// Callers whose event describes a database change should NOT call this directly after
    /// their own commit (a crash in between loses the record). Enqueue the records through
    /// their module's outbox in the same transaction instead — see <see cref="AuditOutbox"/> —
    /// and call this only from the outbox dispatcher, or for facts that involve no database
    /// change at all (rejected / unauthorized deliveries).
    /// </summary>
    public interface IAuditTrail
    {
        Task RecordAsync(IReadOnlyCollection<AuditEventRecord> events, CancellationToken cancellationToken = default);
    }

    /// <summary>Wire format for audit records travelling through a module's transactional outbox.</summary>
    public static class AuditOutbox
    {
        public const string MessageType = "AuditEvents";
    }

    public sealed record AuditOutboxPayload(IReadOnlyList<AuditEventRecord> Events);
}