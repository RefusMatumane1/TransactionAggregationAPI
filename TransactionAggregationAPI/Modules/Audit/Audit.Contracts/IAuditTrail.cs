using System.Data.Common;

namespace Modules.Audit.Contracts
{
    public interface IAuditTrail
    {
        Task RecordAsync(IReadOnlyCollection<AuditEventRecord> events, CancellationToken cancellationToken = default);

        Task RecordWithinAsync(
            IReadOnlyCollection<AuditEventRecord> events, DbTransaction transaction, CancellationToken cancellationToken = default);
    }

    public sealed class UnavailableAuditTrail : IAuditTrail
    {
        public Task RecordAsync(IReadOnlyCollection<AuditEventRecord> events, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("This context was built for design-time tooling and cannot write audit events.");

        public Task RecordWithinAsync(
            IReadOnlyCollection<AuditEventRecord> events, DbTransaction transaction, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("This context was built for design-time tooling and cannot write audit events.");
    }
}