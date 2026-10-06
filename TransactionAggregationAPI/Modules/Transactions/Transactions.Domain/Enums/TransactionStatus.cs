namespace Modules.Transactions.Domain.Enums
{
    // Persisted as integers, never reused. Pending and Expired exist only on pre-ledger rows; no read path returns them.
    public enum TransactionStatus
    {
        Pending = 0,
        Booked = 4,
        Expired = 8
    }
}