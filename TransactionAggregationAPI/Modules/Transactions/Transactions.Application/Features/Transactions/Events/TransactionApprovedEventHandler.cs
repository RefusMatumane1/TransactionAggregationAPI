using MediatR;
using Microsoft.Extensions.Logging;
using Modules.Transactions.Application.Common.Interfaces;
using Modules.Transactions.Domain.Events.Transaction;
using SharedKernel.Common.Interfaces;

namespace Modules.Transactions.Application.Features.Transactions.Events
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

            await _cacheService.RemoveByPatternAsync(
                            $"transactions:{notification.Transaction.CustomerId.Value}*",
                            cancellationToken);

            await _cacheService.RemoveByPatternAsync(
                $"summary:{notification.Transaction.CustomerId.Value}*",
                cancellationToken);

            if (notification.Transaction.Amount.AbsoluteAmount > 10000)
            {
                await _notificationService.SendHighValueTransactionAlertAsync(
                    notification.Transaction,
                    cancellationToken);
            }
        }
    }
}