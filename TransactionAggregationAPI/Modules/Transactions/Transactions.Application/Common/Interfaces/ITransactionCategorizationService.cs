using Modules.Transactions.Domain.Entities;
using Modules.Transactions.Domain.Enums;

namespace Modules.Transactions.Application.Common.Interfaces
{
    public interface ITransactionCategorizationService
    {
        /// <summary>
        /// Our keyword rules win, so a merchant is categorized the same whichever bank it came
        /// through; then the bank's own (normalized) category; then income for money in.
        /// </summary>
        /// <param name="bankCategory">The bank's category mapped to ours during normalization, if any.</param>
        Task<TransactionCategory> CategorizeTransactionAsync(
            Transaction transaction,
            TransactionCategory? bankCategory,
            CancellationToken cancellationToken);
    }
}