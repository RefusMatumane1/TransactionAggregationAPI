using MediatR;
using TransactionAggregation.Application.Common.Behaviors;
using TransactionAggregation.Application.Common.Models;
using TransactionAggregation.Application.Features.Transactions.DTOs;
using TransactionAggregation.Domain.Enums;

namespace TransactionAggregation.Application.Features.Transactions.Queries.GetTransactions
{
    public sealed record GetTransactionsQuery : IRequest<Result<PaginatedResponse<TransactionDto>>>, ICacheableQuery
    {
        // Required
        public required Guid CustomerId { get; init; }

        // Pagination
        public int PageNumber { get; init; } = 1;
        public int PageSize { get; init; } = 20;

        // Filters
        public TransactionCategory? Category { get; init; }
        public TransactionStatus? Status { get; init; }
        public DateTime? FromDate { get; init; }
        public DateTime? ToDate { get; init; }
        public decimal? MinAmount { get; init; }
        public decimal? MaxAmount { get; init; }
        public string? SearchTerm { get; init; }
        public string? Source { get; init; }

        // Sorting
        public string? SortBy { get; init; }
        public bool SortDescending { get; init; } = true;

        // Cache configuration
        public TimeSpan? CacheExpiration => TimeSpan.FromMinutes(5);

        // Validation
        public bool IsValid => PageNumber > 0 && PageSize > 0 && PageSize <= 100;
    }
}
