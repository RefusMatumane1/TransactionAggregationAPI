using Modules.Transactions.Application.Common.Outbox;
using Modules.Transactions.Domain.Entities;
using Modules.Transactions.Domain.Enums;

namespace Modules.Transactions.Application.Common.Interfaces
{
    public interface INotificationService
    {
        Task SendTransactionNotificationAsync(Transaction transaction, NotificationType type, CancellationToken cancellationToken = default);
        Task SendHighValueTransactionAlertAsync(Transaction transaction, CancellationToken cancellationToken = default);
        Task SendFraudAlertAsync(Transaction transaction, string reason, CancellationToken cancellationToken = default);
        Task SendTransactionSummaryAsync(Guid customerId, DailySummary summary, CancellationToken cancellationToken = default);
        Task SendWebhookAsync(string webhookUrl, object payload, CancellationToken cancellationToken = default);
        Task SendDuplicateInboundAlertAsync(DuplicateInboundDetectedOutboxPayload duplicate, CancellationToken cancellationToken = default);
    }

    public enum NotificationType
    {
        TransactionCreated,
        TransactionApproved,
        TransactionRejected,
        TransactionFlagged,
        TransactionSettled,
        TransactionRefunded
    }

    public record DailySummary
    {
        public DateTime Date { get; init; }
        public int TransactionCount { get; init; }
        public decimal TotalSpent { get; init; }
        public decimal TotalIncome { get; init; }
        public Dictionary<TransactionCategory, decimal> CategoryBreakdown { get; init; } = new();
    }
}