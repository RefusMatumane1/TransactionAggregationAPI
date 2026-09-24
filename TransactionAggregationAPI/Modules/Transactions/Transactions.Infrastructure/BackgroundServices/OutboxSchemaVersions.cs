using Modules.Audit.Contracts;
using Modules.Transactions.Application.Common.Outbox;

namespace Modules.Transactions.Infrastructure.BackgroundServices
{
    /// <summary>
    /// The highest payload schema version this dispatcher can read, per outbox message type
    /// (docs/event-contracts.md). Raise a type's entry only together with the code that reads
    /// the new shape; older versions stay readable for as long as rows of them can exist.
    /// </summary>
    internal static class OutboxSchemaVersions
    {
        private static readonly IReadOnlyDictionary<string, int> Readable = new Dictionary<string, int>
        {
            [OutboxMessageTypes.TransactionCreated] = 1,
            [OutboxMessageTypes.TransactionCategorized] = 1,
            [OutboxMessageTypes.TransactionSynced] = 1,
            [OutboxMessageTypes.TransactionsExpired] = 1,
            [OutboxMessageTypes.DuplicateInboundDetected] = 1,
            [AuditOutbox.MessageType] = 1
        };

        /// <summary>
        /// True when this dispatcher knows the type but not this version of it. Unknown types
        /// are left to the dispatcher's own unknown-type handling.
        /// </summary>
        public static bool IsNewerThanReadable(string type, int schemaVersion) =>
            Readable.TryGetValue(type, out var highest) && schemaVersion > highest;
    }
}