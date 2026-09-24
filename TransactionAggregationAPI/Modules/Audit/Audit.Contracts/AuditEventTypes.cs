namespace Modules.Audit.Contracts
{
    public static class AuditEventTypes
    {
        // ── Delivery level: one per webhook call / Kafka record ─────────────
        /// <summary>Accepted and stored in the inbox.</summary>
        public const string InboundReceived = "inbound.received";

        /// <summary>Replay of a delivery already in the inbox — nothing new was queued.</summary>
        public const string InboundDuplicate = "inbound.duplicate";

        /// <summary>Replay of a dead-lettered delivery, which was put back in the queue.</summary>
        public const string InboundRequeued = "inbound.requeued";

        /// <summary>Refused before reaching the inbox (validation failure, malformed Kafka record).</summary>
        public const string InboundRejected = "inbound.rejected";

        /// <summary>Webhook call with a missing or unknown API key.</summary>
        public const string InboundUnauthorized = "inbound.unauthorized";

        // ── Processing level: the inbox dispatcher working a delivery ───────
        public const string InboundProcessed = "inbound.processed";
        public const string InboundProcessingFailed = "inbound.processing_failed";
        public const string InboundDeadLettered = "inbound.dead_lettered";

        // ── Transaction level: one per transaction inside a delivery ────────
        public const string TransactionIngested = "transaction.ingested";

        /// <summary>A stored pending transaction the bank has now posted (amount/date may have changed).</summary>
        public const string TransactionSettled = "transaction.settled";

        /// <summary>Pending past the configured window with no posting — expired by the system.</summary>
        public const string TransactionExpired = "transaction.expired";
        public const string TransactionDuplicateSkipped = "transaction.duplicate_skipped";
    }
}