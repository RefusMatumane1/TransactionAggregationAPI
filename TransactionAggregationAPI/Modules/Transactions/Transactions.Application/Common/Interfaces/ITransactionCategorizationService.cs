using Modules.Transactions.Domain.Enums;

namespace Modules.Transactions.Application.Common.Interfaces
{
    public interface ITransactionCategorizationService
    {
        TransactionCategory Categorize(string description, decimal amount, TransactionCategory? bankCategory);
    }
}