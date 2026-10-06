namespace Modules.Audit.Contracts
{
    public static class AuditChannels
    {
        public const string Webhook = "webhook";
        public const string Kafka = "kafka";

        public const string Admin = "admin";

        public const string Unknown = "unknown";
    }

    public static class AuditSources
    {
        public const string Unauthenticated = "unauthenticated";
    }

    public static class AuditEventIds
    {
        public static Guid Deterministic(string naturalKey)
        {
            var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(naturalKey));
            return new Guid(hash.AsSpan(0, 16));
        }
    }
}