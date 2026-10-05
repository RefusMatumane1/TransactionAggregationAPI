using Modules.Transactions.Application.Common.Outbox;

namespace TransactionAggregation.Worker.Notifications
{
    public interface INotificationService
    {
        Task SendDuplicateInboundAlertAsync(DuplicateInboundDetectedOutboxPayload duplicate, CancellationToken cancellationToken = default);
    }
}