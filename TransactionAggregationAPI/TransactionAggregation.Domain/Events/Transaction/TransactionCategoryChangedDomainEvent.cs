using TransactionAggregation.Domain.Common;
using TransactionAggregation.Domain.Entities;
using TransactionAggregation.Domain.Enums;

namespace TransactionAggregation.Domain.Events.Transaction
{
    public class TransactionCategoryChangedDomainEvent : BaseDomainEvent
    {
        public Entities.Transaction Transaction { get; }
        public TransactionCategory OldCategory { get; }
        public TransactionCategory NewCategory { get; }

        public TransactionCategoryChangedDomainEvent(
            Entities.Transaction transaction,
            TransactionCategory oldCategory,
            TransactionCategory newCategory)
        {
            Transaction = transaction;
            OldCategory = oldCategory;
            NewCategory = newCategory;
        }
    }
}
