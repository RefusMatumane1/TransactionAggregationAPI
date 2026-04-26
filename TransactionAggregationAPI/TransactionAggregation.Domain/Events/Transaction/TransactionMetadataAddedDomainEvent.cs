using TransactionAggregation.Domain.Common;
using TransactionAggregation.Domain.Entities;

namespace TransactionAggregation.Domain.Events.Transaction
{
    public sealed class TransactionMetadataAddedDomainEvent : BaseDomainEvent
    {
        public Entities.Transaction Transaction { get; }
        public string Key { get; }
        public string Value { get; }

        public TransactionMetadataAddedDomainEvent(Entities.Transaction transaction, string key, string value)
        {
            Transaction = transaction;
            Key = key;
            Value = value;
        }
    }
}
