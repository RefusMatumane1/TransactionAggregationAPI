namespace Modules.Transactions.Domain.Enums
{
    public enum TransactionStatus
    {
        Pending = 0,
        Approved = 1,
        Rejected = 2,
        Flagged = 3,
        Settled = 4,
        Refunded = 5,
        Disputed = 6,
        Cancelled = 7,

        /// <summary>
        /// Pending for longer than the configured window with no posting from the bank — most
        /// likely a dropped authorisation. Unlike Cancelled this is our inference, not the
        /// bank's word: a later posting still settles it.
        /// </summary>
        Expired = 8
    }
}