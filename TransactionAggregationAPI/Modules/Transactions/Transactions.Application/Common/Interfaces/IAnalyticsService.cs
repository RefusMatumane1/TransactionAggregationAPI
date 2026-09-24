using Modules.Transactions.Domain.Entities;
using Modules.Transactions.Domain.Enums;

namespace Modules.Transactions.Application.Common.Interfaces;

public interface IAnalyticsService
{
    Task TrackTransactionCreatedAsync(Transaction transaction, CancellationToken cancellationToken = default);
    Task TrackTransactionCategorizedAsync(Transaction transaction, TransactionCategory oldCategory, TransactionCategory newCategory, bool isAuto, CancellationToken cancellationToken = default);
    Task TrackTransactionSyncedAsync(Transaction transaction, CancellationToken cancellationToken = default);
}