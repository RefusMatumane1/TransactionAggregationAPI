using Modules.Audit.Contracts;
using Modules.Customers.Domain;
using System.Diagnostics;

namespace Modules.Customers.Application.Common
{
    // An administrative change to a customer, attributed to the signed-in user. The customer is
    // identified by id and reference only: the name is personal data and stays out of the trail.
    internal static class CustomerAudit
    {
        // The audit trail's SourceName for changes that concern no particular bank.
        public const string Registry = "customers";

        public static AuditEventRecord Of(string eventType, Customer customer, Guid actor, string detail,
            string? institution = null, string? externalAccountId = null) =>
            new(
                EventId: Guid.NewGuid(),
                EventType: eventType,
                OccurredAt: DateTime.UtcNow,
                Channel: AuditChannels.Admin,
                SourceName: institution ?? Registry,
                ExternalAccountId: externalAccountId,
                Detail: detail,
                Metadata: new Dictionary<string, string>
                {
                    ["customerId"] = customer.Id.Value.ToString(),
                    ["customerReference"] = customer.Reference
                },
                TraceId: Activity.Current?.TraceId.ToString(),
                Actor: actor.ToString());
    }
}