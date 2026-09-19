using MediatR;
using SharedKernel.Common.Behaviors;
using SharedKernel.Common.Models;
using TransactionAggregation.Application.Common.Models;
using TransactionAggregation.Application.Features.Transactions.DTOs;
using TransactionAggregation.Domain.Enums;

namespace TransactionAggregation.Application.Features.Transactions.Queries.GetTransactions
{
    public sealed record GetTransactionsQuery : IRequest<Result<PaginatedResponse<TransactionDto>>>, ICacheableQuery, ICacheKeyPrefix
    {

        public required Guid CustomerId { get; init; }

        public string CachePrefix => $"transactions:{CustomerId}";

        public int PageNumber { get; init; } = 1;
        public int PageSize { get; init; } = 20;

        public TransactionCategory? Category { get; init; }
        public TransactionStatus? Status { get; init; }
        public DateTime? FromDate { get; init; }
        public DateTime? ToDate { get; init; }
        public decimal? MinAmount { get; init; }
        public decimal? MaxAmount { get; init; }
        public string? SearchTerm { get; init; }
        public string? Source { get; init; }

        public string? SortBy { get; init; }
        public bool SortDescending { get; init; } = true;

        public TimeSpan? CacheExpiration => TimeSpan.FromMinutes(5);

        public bool IsValid => PageNumber > 0 && PageSize > 0 && PageSize <= 100;
    }
}