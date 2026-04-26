using TransactionAggregation.Domain.Common;
using TransactionAggregation.Domain.Entities;

namespace TransactionAggregation.Domain.Events.Transaction
{
    public class TransactionCreatedDomainEvent : BaseDomainEvent
    {
        public Entities.Transaction Transaction { get; }

        public TransactionCreatedDomainEvent(Entities.Transaction transaction)
        {
            Transaction = transaction;
        }
    }
}
