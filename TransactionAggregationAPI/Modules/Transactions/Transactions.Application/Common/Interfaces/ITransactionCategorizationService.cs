using Modules.Transactions.Domain.Enums;

namespace Modules.Transactions.Application.Common.Interfaces
{
    public interface ITransactionCategorizationService
    {
        // Decided once, at recording; a category never changes.
        TransactionCategory Categorize(string description, decimal amount, TransactionCategory? bankCategory);
    }
}