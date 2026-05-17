using MediatR;
using Microsoft.Extensions.Logging;
using TransactionAggregation.Application.Common.Interfaces;
using TransactionAggregation.Domain.Events.Transaction;

namespace TransactionAggregation.Application.Features.Transactions.Events
{
    public class TransactionCategorizedEventHandler(
        ILogger<TransactionCategorizedEventHandler> _logger,
        ICacheService _cacheService,
        IAnalyticsService _analyticsService,
        INotificationService _notificationService)
        : INotificationHandler<TransactionCategorizedDomainEvent>
    {
        public async Task Handle(TransactionCategorizedDomainEvent notification, CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "Transaction {TransactionId} recategorized from {OldCategory} to {NewCategory}. Auto: {IsAuto}",
                notification.Transaction.Id.Value,
                notification.OldCategory,
                notification.NewCategory,
                notification.IsAutoCategorized);

            await _cacheService.RemoveByPatternAsync(
                $"transactions:{notification.Transaction.CustomerId.Value}*",
                cancellationToken);

            await _cacheService.RemoveAsync(
                $"summary:{notification.Transaction.CustomerId.Value}",
                cancellationToken);

            await _analyticsService.TrackTransactionCategorizedAsync(
                notification.Transaction,
                notification.OldCategory,
                notification.NewCategory,
                notification.IsAutoCategorized,
                cancellationToken);

            // Notify on manual recategorization so the customer knows their category was changed
            if (!notification.IsAutoCategorized)
            {
                await _notificationService.SendTransactionNotificationAsync(
                    notification.Transaction,
                    NotificationType.TransactionCreated,
                    cancellationToken);
            }
        }
    }
}
