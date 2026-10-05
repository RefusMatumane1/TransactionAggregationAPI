namespace Modules.Transactions.Domain.Enums
{
    // Values are persisted as integers and never reused. Every row written since the ledger became
    // insert-only is Booked; Pending and Expired exist only on rows stored before that and
    // are not ledger entries, so no read path returns them.
    public enum TransactionStatus
    {
        Pending = 0,
        Booked = 4,
        Expired = 8
    }
}