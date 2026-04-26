using TransactionAggregation.Domain.Common;
using TransactionAggregation.Domain.Entities;
using TransactionAggregation.Domain.Enums;

namespace TransactionAggregation.Domain.Events.Transaction
{
    public sealed class TransactionCategorizedDomainEvent : BaseDomainEvent
    {
        public Entities.Transaction Transaction { get; }
        public TransactionCategory OldCategory { get; }
        public TransactionCategory NewCategory { get; }
        public string? Reason { get; }
        public bool IsAutoCategorized { get; }

        public TransactionCategorizedDomainEvent(
            Entities.Transaction transaction,
            TransactionCategory oldCategory,
            TransactionCategory newCategory,
            string? reason = null,
            bool isAutoCategorized = false)
        {
            Transaction = transaction;
            OldCategory = oldCategory;
            NewCategory = newCategory;
            Reason = reason;
            IsAutoCategorized = isAutoCategorized;
        }
    }
}
