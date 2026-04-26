using MediatR;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using TransactionAggregation.Application.Common.Interfaces;
using TransactionAggregation.Domain.Events.Transaction;

namespace TransactionAggregation.Application.Features.Transactions.Events
{
    public class TransactionCategorizedEventHandler(ILogger<TransactionCategorizedEventHandler> _logger,
       ICacheService _cacheService,
       IAnalyticsService _analyticsService)
        : INotificationHandler<TransactionCategorizedDomainEvent>
    {
        public async Task Handle(TransactionCategorizedDomainEvent notification, CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "Transaction {TransactionId} recategorized from {OldCategory} to {NewCategory}. Reason: {Reason}, Auto: {IsAuto}",
                notification.Transaction.Id.Value,
                notification.OldCategory,
                notification.NewCategory,
                notification.Reason ?? "Manual",
                notification.IsAutoCategorized);

            // Invalidate caches
            await _cacheService.RemoveByPatternAsync(
                $"transactions:{notification.Transaction.CustomerId.Value}*",
                cancellationToken);

            await _cacheService.RemoveAsync(
                $"summary:{notification.Transaction.CustomerId.Value}",
                cancellationToken);

            // Track for ML model training
            if (notification.IsAutoCategorized)
            {
                await _analyticsService.TrackAutoCategorizationAsync(
                    notification.Transaction,
                    notification.OldCategory,
                    notification.NewCategory,
                    cancellationToken);
            }
        }
    }
}
