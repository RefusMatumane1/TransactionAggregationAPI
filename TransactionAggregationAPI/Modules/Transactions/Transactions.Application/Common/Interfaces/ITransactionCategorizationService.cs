using Modules.Transactions.Domain.Entities;
using Modules.Transactions.Domain.Enums;

namespace Modules.Transactions.Application.Common.Interfaces
{
    public interface ITransactionCategorizationService
    {
        Task<TransactionCategory> CategorizeTransactionAsync(Transaction transaction,
            CancellationToken cancellationToken);
    }
}