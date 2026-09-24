namespace Modules.Audit.Contracts
{
    /// <summary>
    /// How a delivery reached the system. Processing-time events (ingested, failed, …) carry
    /// the channel of the delivery they belong to, so filtering by channel shows the whole story.
    /// </summary>
    public static class AuditChannels
    {
        public const string Webhook = "webhook";
        public const string Kafka = "kafka";

        /// <summary>Deliveries queued before the channel was captured.</summary>
        public const string Unknown = "unknown";

        /// <summary>
        /// Not an inbound delivery at all: a decision the system made on its own, e.g. the
        /// pending-expiry job giving up on an authorisation the bank never posted.
        /// </summary>
        public const string System = "system";
    }

    public static class AuditEventIds
    {
        /// <summary>
        /// A stable id derived from a natural key (e.g. "kafka:topic:partition:offset:rejected"),
        /// for producers that may retry recording the same fact and have nowhere to persist a
        /// random id in between — the retry maps to the same EventId and is stored once.
        /// </summary>
        public static Guid Deterministic(string naturalKey)
        {
            var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(naturalKey));
            return new Guid(hash.AsSpan(0, 16));
        }
    }
}