using MediatR;
using Microsoft.Extensions.Logging;
using TransactionAggregation.Application.Common.Interfaces;
using TransactionAggregation.Domain.Events.Transaction;

namespace TransactionAggregation.Application.Features.Transactions.Events
{
    public class TransactionSyncedEventHandler(ILogger<TransactionSyncedEventHandler> _logger,
        ICacheService _cacheService)
        //,IAnalyticsService _analyticsService)
        : INotificationHandler<TransactionSyncedEvent>
    {
        public async Task Handle(TransactionSyncedEvent notification, CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "Transaction {TransactionId} synced for customer {CustomerId} from {SyncSource}",
                notification.Transaction.Id.Value,
                notification.Transaction.CustomerId.Value,
                notification.SyncSource);

            // Invalidate cache
            await _cacheService.RemoveByPatternAsync(
                $"transactions:{notification.Transaction.CustomerId.Value}*",
                cancellationToken);

            await _cacheService.RemoveAsync(
                $"summary:{notification.Transaction.CustomerId.Value}",
                cancellationToken);

            // Track analytics
            //await _analyticsService.TrackTransactionSyncedAsync(
            //    notification.Transaction,
            //    cancellationToken);
        }
    }
}
