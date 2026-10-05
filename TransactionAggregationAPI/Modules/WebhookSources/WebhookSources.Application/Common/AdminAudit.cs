using Modules.Audit.Contracts;
using Modules.WebhookSources.Domain;
using System.Diagnostics;

namespace Modules.WebhookSources.Application.Common
{
    // An administrative change, attributed to the signed-in user who made it. Staged on the context so
    // it commits in the same transaction as the change it describes.
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