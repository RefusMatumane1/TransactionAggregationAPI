using Modules.Transactions.Domain.Enums;

namespace Modules.Transactions.Application.Common.Interfaces
{
    public interface ITransactionCategorizationService
    {
        // Decided once, when the transaction is recorded; a ledger entry's category never changes.
        TransactionCategory Categorize(string description, decimal amount, TransactionCategory? bankCategory);
    }
}