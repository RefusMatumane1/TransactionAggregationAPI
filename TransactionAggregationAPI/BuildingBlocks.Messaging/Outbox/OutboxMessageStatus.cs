namespace BuildingBlocks.Messaging.Outbox
{
    public enum OutboxMessageStatus
    {
        Pending = 0,

        Processing = 1,

        Processed = 2,

        DeadLettered = 3
    }
}
