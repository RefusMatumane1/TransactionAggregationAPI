using System.Diagnostics;

namespace Modules.Audit.Contracts
{
    // Who looked at a customer's financial data. Reads leave no other trace, so each one is recorded here.
    public static class AccessAudit
    {
        public const string CustomersRegistry = "customers";

        public static AuditEventRecord CustomerViewed(Guid actor, Guid customerId, string view, string institutionScope) =>
            new(
                EventId: Guid.NewGuid(),
                EventType: AuditEventTypes.CustomerViewed,
                OccurredAt: DateTime.UtcNow,
                Channel: AuditChannels.Api,
                SourceName: CustomersRegistry,
                Detail: $"Viewed {view}",
                Metadata: new Dictionary<string, string>
                {
                    ["customerId"] = customerId.ToString(),
                    ["view"] = view,
                    ["institutionScope"] = institutionScope
                },
                TraceId: Activity.Current?.TraceId.ToString(),
                Actor: actor.ToString());
    }
}
