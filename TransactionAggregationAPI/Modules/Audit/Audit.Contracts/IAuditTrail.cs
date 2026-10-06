using System.Data.Common;

namespace Modules.Audit.Contracts
{
    public interface IAuditTrail
    {
        Task RecordAsync(IReadOnlyCollection<AuditEventRecord> events, CancellationToken cancellationToken = default);

        // Writes inside the caller's open transaction, on the shared scoped connection.
        Task RecordWithinAsync(
            IReadOnlyCollection<AuditEventRecord> events, DbTransaction transaction, CancellationToken cancellationToken = default);
    }

    // For EF design-time tooling, which never saves.
    public sealed class UnavailableAuditTrail : IAuditTrail
    {
        public Task RecordAsync(IReadOnlyCollection<AuditEventRecord> events, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("This context was built for design-time tooling and cannot write audit events.");

        public Task RecordWithinAsync(
            IReadOnlyCollection<AuditEventRecord> events, DbTransaction transaction, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("This context was built for design-time tooling and cannot write audit events.");
    }
}