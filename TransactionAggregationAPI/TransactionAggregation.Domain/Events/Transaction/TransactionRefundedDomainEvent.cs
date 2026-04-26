using TransactionAggregation.Domain.Common;
using TransactionAggregation.Domain.Entities;
using TransactionAggregation.Domain.Enums;

namespace TransactionAggregation.Domain.Events.Transaction
{
    public sealed class TransactionRefundedDomainEvent : BaseDomainEvent
    {
        public Entities.Transaction Transaction { get; }
        public string Reason { get; }
        public TransactionStatus OldStatus { get; }
        public decimal RefundAmount { get; }

        public TransactionRefundedDomainEvent(
            Entities.Transaction transaction,
            string reason,
            TransactionStatus oldStatus)
        {
            Transaction = transaction;
            Reason = reason;
            OldStatus = oldStatus;
            RefundAmount = transaction.Amount.AbsoluteAmount;
        }
    }
}
