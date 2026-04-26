using TransactionAggregation.Domain.Entities;
using TransactionAggregation.Domain.Enums;

namespace TransactionAggregation.Application.Common.Interfaces
{
    /// <summary>
    /// Service for sending notifications (email, SMS, push, webhook)
    /// </summary>
    public interface INotificationService
    {
        Task SendTransactionNotificationAsync(Transaction transaction, NotificationType type, CancellationToken cancellationToken = default);
        Task SendHighValueTransactionAlertAsync(Transaction transaction, CancellationToken cancellationToken = default);
        Task SendFraudAlertAsync(Transaction transaction, string reason, CancellationToken cancellationToken = default);
        Task SendTransactionSummaryAsync(Guid customerId, DailySummary summary, CancellationToken cancellationToken = default);
        Task SendWebhookAsync(string webhookUrl, object payload, CancellationToken cancellationToken = default);
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
