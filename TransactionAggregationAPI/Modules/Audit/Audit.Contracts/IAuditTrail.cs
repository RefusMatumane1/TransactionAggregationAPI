using System.Data.Common;

namespace Modules.Audit.Contracts
{
    public interface IAuditTrail
    {
        Task RecordAsync(IReadOnlyCollection<AuditEventRecord> events, CancellationToken cancellationToken = default);

        // Writes the events inside the caller's open transaction, so they commit or roll back with
        // the caller's own changes. The transaction must be on the scoped connection the module shares.
        Task RecordWithinAsync(
            IReadOnlyCollection<AuditEventRecord> events, DbTransaction transaction, CancellationToken cancellationToken = default);
    }

    // For design-time tooling (EF migrations), which builds contexts but never saves through them.
    public sealed class UnavailableAuditTrail : IAuditTrail
    {
        public Task RecordAsync(IReadOnlyCollection<AuditEventRecord> events, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("This context was built for design-time tooling and cannot write audit events.");

        public Task RecordWithinAsync(
            IReadOnlyCollection<AuditEventRecord> events, DbTransaction transaction, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("This context was built for design-time tooling and cannot write audit events.");
    }
}