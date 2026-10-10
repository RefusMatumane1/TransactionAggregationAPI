using Modules.Audit.Contracts;
using Modules.WebhookSources.Domain;
using System.Diagnostics;

namespace Modules.WebhookSources.Application.Common
{
    internal static class AdminAudit
    {
        public static AuditEventRecord Of(string eventType, WebhookSource source, Guid actor, string detail) =>
            new(
                EventId: Guid.NewGuid(),
                EventType: eventType,
                OccurredAt: DateTime.UtcNow,
                Channel: AuditChannels.Admin,
                SourceName: source.Name,
                Detail: detail,
                Metadata: new Dictionary<string, string> { ["sourceId"] = source.Id.Value.ToString() },
                TraceId: Activity.Current?.TraceId.ToString(),
                Actor: actor.ToString());
    }
}