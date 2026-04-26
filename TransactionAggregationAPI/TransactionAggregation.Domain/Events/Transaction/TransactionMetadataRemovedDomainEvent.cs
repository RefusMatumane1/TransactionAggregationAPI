using TransactionAggregation.Domain.Common;
using TransactionAggregation.Domain.Entities;

namespace TransactionAggregation.Domain.Events.Transaction
{
    public sealed class TransactionMetadataRemovedDomainEvent : BaseDomainEvent
    {
        public Entities.Transaction Transaction { get; }
        public string Key { get; }

        public TransactionMetadataRemovedDomainEvent(Entities.Transaction transaction, string key)
        {
            Transaction = transaction;
            Key = key;
        }
    }
}
