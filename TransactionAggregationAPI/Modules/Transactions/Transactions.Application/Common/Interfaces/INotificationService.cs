using Modules.Transactions.Application.Common.Outbox;
using Modules.Transactions.Domain.Entities;

namespace Modules.Transactions.Application.Common.Interfaces
{
    public interface INotificationService
    {
        Task SendTransactionNotificationAsync(Transaction transaction, NotificationType type, CancellationToken cancellationToken = default);
        Task SendDuplicateInboundAlertAsync(DuplicateInboundDetectedOutboxPayload duplicate, CancellationToken cancellationToken = default);
    }

    public enum NotificationType
    {
        TransactionCreated,
        TransactionCategorized
    }
}