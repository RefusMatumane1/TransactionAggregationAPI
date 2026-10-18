using Modules.Audit.Contracts;
using System.Diagnostics;

namespace Modules.Notifications.Application.Common
{
    internal static class AdminAudit
    {
        public static AuditEventRecord Of(string eventType, string bankCode, Guid actor, string detail) =>
            new(
                EventId: Guid.NewGuid(),
                EventType: eventType,
                OccurredAt: DateTime.UtcNow,
                Channel: AuditChannels.Admin,
                SourceName: bankCode,
                Detail: detail,
                TraceId: Activity.Current?.TraceId.ToString(),
                Actor: actor.ToString());
    }
}