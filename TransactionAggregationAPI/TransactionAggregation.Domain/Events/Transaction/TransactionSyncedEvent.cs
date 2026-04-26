using TransactionAggregation.Domain.Common;
using TransactionAggregation.Domain.Entities;

namespace TransactionAggregation.Domain.Events.Transaction
{
    public sealed class TransactionSyncedEvent : BaseDomainEvent
    {
        public Entities.Transaction Transaction { get; }
        public DateTime SyncDate { get; }
        public string SyncSource { get; }

        public TransactionSyncedEvent(Entities.Transaction transaction, string syncSource = "Manual")
        {
            Transaction = transaction;
            SyncDate = DateTime.UtcNow;
            SyncSource = syncSource;
        }
    }
}
