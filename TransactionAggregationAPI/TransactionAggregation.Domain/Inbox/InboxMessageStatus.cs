namespace TransactionAggregation.Domain.Inbox
{
    public enum InboxMessageStatus
    {
        /// <summary>Not yet claimed by any dispatcher, or claimed then failed and waiting on
        /// NextAttemptAt for its next try.</summary>
        Pending = 0,

        /// <summary>Claimed by a dispatcher instance and being processed. A row stuck here past
        /// a staleness window (dispatcher crashed mid-batch) is reclaimable by another poll.</summary>
        Processing = 1,

        Processed = 2,

        /// <summary>Failed Inbox:MaxAttempts times — kept, not deleted, so a permanently-failing
        /// message stays visible and investigable rather than silently disappearing.</summary>
        DeadLettered = 3
    }
}
