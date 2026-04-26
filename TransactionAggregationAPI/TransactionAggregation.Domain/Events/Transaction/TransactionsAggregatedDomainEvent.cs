using TransactionAggregation.Domain.Common;

namespace TransactionAggregation.Domain.Events.Transaction
{
    public class TransactionsAggregatedDomainEvent : BaseDomainEvent
    {
        public Guid CustomerId { get; }
        public int TransactionCount { get; }
        public DateTime AggregationDate { get; }

        public TransactionsAggregatedDomainEvent(
            Guid customerId,
            int transactionCount)
        {
            CustomerId = customerId;
            TransactionCount = transactionCount;
            AggregationDate = DateTime.UtcNow;
        }
    }
}
