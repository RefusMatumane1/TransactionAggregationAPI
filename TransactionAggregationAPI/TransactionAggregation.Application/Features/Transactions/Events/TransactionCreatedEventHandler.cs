using MediatR;
using Microsoft.Extensions.Logging;
using TransactionAggregation.Application.Common.Interfaces;
using TransactionAggregation.Domain.Events.Transaction;

namespace TransactionAggregation.Application.Features.Transactions.Events
{
    public class TransactionCreatedEventHandler(
        ILogger<TransactionCreatedEventHandler> _logger,
        ITransactionCategorizationService _categorizationService,
        IAnalyticsService _analyticsService,
        INotificationService _notificationService)
        : INotificationHandler<TransactionCreatedDomainEvent>
    {
        public async Task Handle(TransactionCreatedDomainEvent notification, CancellationToken cancellationToken)
        {
            var transaction = notification.Transaction;

            _logger.LogInformation(
                "Transaction {TransactionId} created for customer {CustomerId}, Amount: {Amount}",
                transaction.Id.Value,
                transaction.CustomerId.Value,
                transaction.Amount.Formatted);

            // Auto-categorize — change is tracked by EF Core and saved after this handler returns
            var category = await _categorizationService.CategorizeTransactionAsync(transaction, cancellationToken);
            transaction.Categorize(category, isAuto: true);

            await _analyticsService.TrackTransactionCreatedAsync(transaction, cancellationToken);

            await _notificationService.SendTransactionNotificationAsync(
                transaction, NotificationType.TransactionCreated, cancellationToken);
        }
    }
}
