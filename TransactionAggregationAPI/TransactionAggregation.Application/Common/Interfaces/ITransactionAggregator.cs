using System;
using System.Collections.Generic;
using System.Text;
using TransactionAggregation.Domain.Entities;

namespace TransactionAggregation.Application.Common.Interfaces
{
    public interface ITransactionAggregator
    {
        Task<AggregationResult> AggregateCustomerTransactionsAsync(
            Guid customerId,
            DateTime? fromDate,
            DateTime? toDate,
            CancellationToken cancellationToken = default);
    }

    public sealed record AggregationResult
    {
        public IReadOnlyList<Transaction> Transactions { get; init; } = Array.Empty<Transaction>();
        public IReadOnlyList<SourceAggregationResult> SourceResults { get; init; } = Array.Empty<SourceAggregationResult>();
        public int FailedSourceCount => SourceResults.Count(s => !s.IsSuccess);
    }

    public sealed record SourceAggregationResult
    {
        public string SourceName { get; init; } = null!;
        public int TransactionsFound { get; init; }
        public bool IsSuccess { get; init; }
        public string? Error { get; init; }
        public TimeSpan Duration { get; init; }
    }
}
