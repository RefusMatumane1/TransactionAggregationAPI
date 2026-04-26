namespace TransactionAggregation.Domain.Enums
{
    public enum TransactionStatus
    {
        Pending = 0,      // Initial state, waiting for processing
        Approved = 1,     // Transaction is valid and approved
        Rejected = 2,     // Transaction is invalid or fraudulent
        Flagged = 3,      // Suspicious transaction needs review
        Settled = 4,      // Transaction completed and settled
        Refunded = 5,     // Transaction was refunded
        Disputed = 6,     // Customer disputed the transaction
        Cancelled = 7     // Transaction was cancelled before completion
    }
}
