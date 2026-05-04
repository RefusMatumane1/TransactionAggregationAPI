namespace TransactionAggregationUI.Models.Transactions;

public enum TransactionStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2,
    Flagged = 3,
    Settled = 4,
    Refunded = 5,
    Disputed = 6,
    Cancelled = 7
}
