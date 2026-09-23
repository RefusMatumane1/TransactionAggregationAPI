using SharedKernel.Common;
using Modules.Transactions.Domain.Entities;
using Modules.Transactions.Domain.Enums;

namespace Modules.Transactions.Domain.Events.Transaction
{
    public sealed class TransactionFlaggedDomainEvent : BaseDomainEvent
    {
        public Entities.Transaction Transaction { get; }
        public string Reason { get; }
        public TransactionStatus OldStatus { get; }
        public bool RequiresInvestigation { get; }

        public TransactionFlaggedDomainEvent(
            Entities.Transaction transaction,
            string reason,
            TransactionStatus oldStatus,
            bool requiresInvestigation = true)
        {
            Transaction = transaction;
            Reason = reason;
            OldStatus = oldStatus;
            RequiresInvestigation = requiresInvestigation;
        }
    }
}