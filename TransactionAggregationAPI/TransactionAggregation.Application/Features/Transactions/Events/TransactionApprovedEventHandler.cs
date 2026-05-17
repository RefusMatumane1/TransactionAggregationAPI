using MediatR;
using Microsoft.Extensions.Logging;
using TransactionAggregation.Application.Common.Interfaces;
using TransactionAggregation.Domain.Events.Transaction;

namespace TransactionAggregation.Application.Features.Transactions.Events
{
    public class TransactionApprovedEventHandler(ILogger<TransactionApprovedEventHandler> _logger,
        ICacheService _cacheService,
        INotificationService _notificationService)
        : INotificationHandler<TransactionApprovedDomainEvent>
    {
        public async Task Handle(TransactionApprovedDomainEvent notification, CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "Transaction {TransactionId} approved by {ApprovedBy} at {ApprovedAt}",
                notification.Transaction.Id.Value,
                notification.ApprovedBy,
                notification.ApprovedAt);

            // Invalidate caches
            await _cacheService.RemoveByPatternAsync(
                $"transactions:{notification.Transaction.CustomerId.Value}*",
                cancellationToken);

            await _cacheService.RemoveAsync(
                $"summary:{notification.Transaction.CustomerId.Value}",
                cancellationToken);

            // Send notification if transaction is large
            if (notification.Transaction.Amount.AbsoluteAmount > 10000)
            {
                await _notificationService.SendHighValueTransactionAlertAsync(
                    notification.Transaction,
                    cancellationToken);
            }
        }
    }
}
