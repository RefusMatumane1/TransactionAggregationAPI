using TransactionAggregation.Domain.Common;
using TransactionAggregation.Domain.Entities;
using TransactionAggregation.Domain.Enums;

namespace TransactionAggregation.Domain.Events.Transaction
{
    public sealed class TransactionApprovedDomainEvent : BaseDomainEvent
    {
        public Entities.Transaction Transaction { get; }
        public TransactionStatus OldStatus { get; }
        public string ApprovedBy { get; }
        public DateTime ApprovedAt { get; }

        public TransactionApprovedDomainEvent(
            Entities.Transaction transaction,
            TransactionStatus oldStatus,
            string approvedBy)
        {
            Transaction = transaction;
            OldStatus = oldStatus;
            ApprovedBy = approvedBy;
            ApprovedAt = DateTime.UtcNow;
        }
    }
}
