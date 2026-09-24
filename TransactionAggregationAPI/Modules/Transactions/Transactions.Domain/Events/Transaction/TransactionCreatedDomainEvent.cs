using SharedKernel.Common;

namespace Modules.Transactions.Domain.Events.Transaction
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