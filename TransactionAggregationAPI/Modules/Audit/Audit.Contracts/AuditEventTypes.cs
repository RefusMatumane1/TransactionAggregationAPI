namespace Modules.Audit.Contracts
{
    public static class AuditEventTypes
    {
        public const string InboundReceived = "inbound.received";
        public const string InboundDuplicate = "inbound.duplicate";
        public const string InboundRequeued = "inbound.requeued";
        public const string InboundRejected = "inbound.rejected";
        public const string InboundUnauthorized = "inbound.unauthorized";
        public const string InboundProcessed = "inbound.processed";
        public const string InboundProcessingFailed = "inbound.processing_failed";
        public const string InboundDeadLettered = "inbound.dead_lettered";

        public const string TransactionIngested = "transaction.ingested";
        public const string TransactionDuplicateSkipped = "transaction.duplicate_skipped";
        public const string TransactionPendingSkipped = "transaction.pending_skipped";

        public const string SourceCreated = "admin.source_created";
        public const string SourceUpdated = "admin.source_updated";
        public const string SourceActivated = "admin.source_activated";
        public const string SourceDeactivated = "admin.source_deactivated";
        public const string SourceKeyRotated = "admin.source_key_rotated";
        public const string SourceSigningKeyRegistered = "admin.source_signing_key_registered";

        public const string CustomerCreated = "admin.customer_created";
        public const string CustomerAccountLinked = "admin.customer_account_linked";
        public const string CustomerAccountUnlinked = "admin.customer_account_unlinked";
    }
}