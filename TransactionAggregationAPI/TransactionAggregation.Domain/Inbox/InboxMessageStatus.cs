namespace TransactionAggregation.Domain.Inbox
{
    public enum InboxMessageStatus
    {
        Pending = 0,

        Processing = 1,

        Processed = 2,

        DeadLettered = 3
    }
}