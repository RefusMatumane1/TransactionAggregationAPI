using TransactionAggregation.Domain.Common;
using TransactionAggregation.Domain.Entities;
using TransactionAggregation.Domain.Enums;

namespace TransactionAggregation.Domain.Events.Transaction
{
    public sealed class TransactionSettledDomainEvent : BaseDomainEvent
    {
        public Entities.Transaction Transaction { get; }
        public TransactionStatus OldStatus { get; }
        public DateTime SettledAt { get; }

        public TransactionSettledDomainEvent(
            Entities.Transaction transaction,
            TransactionStatus oldStatus)
        {
            Transaction = transaction;
            OldStatus = oldStatus;
            SettledAt = DateTime.UtcNow;
        }
    }
}
