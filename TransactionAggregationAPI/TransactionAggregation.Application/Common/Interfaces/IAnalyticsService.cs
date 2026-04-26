using TransactionAggregation.Domain.Entities;
using TransactionAggregation.Domain.Enums;

namespace TransactionAggregation.Application.Common.Interfaces;

/// <summary>
/// Service for tracking analytics and business metrics
/// </summary>
public interface IAnalyticsService
{
    Task TrackTransactionCreatedAsync(Transaction transaction, CancellationToken cancellationToken = default);
    Task TrackTransactionApprovedAsync(Transaction transaction, string approvedBy, CancellationToken cancellationToken = default);
    Task TrackTransactionRejectedAsync(Transaction transaction, string reason, CancellationToken cancellationToken = default);
    Task TrackTransactionCategorizedAsync(Transaction transaction, TransactionCategory oldCategory, TransactionCategory newCategory, bool isAuto, CancellationToken cancellationToken = default);
    Task TrackAutoCategorizationAsync(Transaction transaction, TransactionCategory oldCategory, TransactionCategory newCategory, CancellationToken cancellationToken = default);
    Task TrackTransactionSyncedAsync(Transaction transaction, CancellationToken cancellationToken = default);
    Task TrackMetricAsync(string metricName, double value, Dictionary<string, string>? tags = null, CancellationToken cancellationToken = default);
    Task TrackUserBehaviorAsync(Guid customerId, string action, Dictionary<string, object>? properties = null, CancellationToken cancellationToken = default);
    Task TrackPerformanceAsync(string operationName, TimeSpan duration, bool isSuccess = true, CancellationToken cancellationToken = default);
    Task TrackErrorAsync(Exception exception, string context, Dictionary<string, string>? metadata = null, CancellationToken cancellationToken = default);
}