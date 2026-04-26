using TransactionAggregation.Domain.Common;
using TransactionAggregation.Domain.Entities;
using TransactionAggregation.Domain.Enums;

namespace TransactionAggregation.Domain.Events.Transaction
{
    public sealed class TransactionRejectedDomainEvent : BaseDomainEvent
    {
        public Entities.Transaction Transaction { get; }
        public string Reason { get; }
        public string RejectedBy { get; }
        public TransactionStatus OldStatus { get; }
        public bool RequiresManualReview { get; }

        public TransactionRejectedDomainEvent(
            Entities.Transaction transaction,
            string reason,
            string rejectedBy,
            TransactionStatus oldStatus,
            bool requiresManualReview = false)
        {
            Transaction = transaction;
            Reason = reason;
            RejectedBy = rejectedBy;
            OldStatus = oldStatus;
            RequiresManualReview = requiresManualReview;
        }
    }
}
