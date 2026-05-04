using MapsterMapper;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TransactionAggregation.Application.Common.Interfaces;
using TransactionAggregation.Application.Common.Models;
using TransactionAggregation.Application.Features.Transactions.DTOs;
using TransactionAggregation.Domain.Common.ValueObjects;
using TransactionAggregation.Domain.Entities;

namespace TransactionAggregation.Application.Features.Transactions.Queries.GetTransactions
{
    public sealed class GetTransactionsQueryHandler : IRequestHandler<GetTransactionsQuery, Result<PaginatedResponse<TransactionDto>>>
    {
        private readonly IApplicationDbContext _context;
        private readonly IMapper _mapper;
        private readonly ILogger<GetTransactionsQueryHandler> _logger;

        public GetTransactionsQueryHandler(
            IApplicationDbContext context,
            IMapper mapper,
            ILogger<GetTransactionsQueryHandler> logger)
        {
            _context = context;
            _mapper = mapper;
            _logger = logger;
        }

        public async Task<Result<PaginatedResponse<TransactionDto>>> Handle(
            GetTransactionsQuery request,
            CancellationToken cancellationToken)
        {
            try
            {
                var query = _context.Transactions
                    .Where(t => t.CustomerId == CustomerId.CreateFrom(request.CustomerId))
                    .AsNoTracking();

                // Apply filters
                query = ApplyFilters(query, request);

                // Apply sorting
                query = ApplySorting(query, request);

                // Get total count
                var totalCount = await query.CountAsync(cancellationToken);

                // Apply pagination
                var items = await query
                    .Skip((request.PageNumber - 1) * request.PageSize)
                    .Take(request.PageSize)
                    .ToListAsync(cancellationToken);

                // Map to DTOs
                var dtos = _mapper.Map<List<TransactionDto>>(items);

                var response = PaginatedResponse<TransactionDto>.Create(
                    dtos,
                    totalCount,
                    request.PageNumber,
                    request.PageSize);

                return Result<PaginatedResponse<TransactionDto>>.Success(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting transactions for customer {CustomerId}", request.CustomerId);
                return Result.Failure<PaginatedResponse<TransactionDto>>(
                    Error.Failure("QueryFailed", $"Failed to retrieve transactions: {ex.Message}"));
            }
        }

        private static IQueryable<Transaction> ApplyFilters(
            IQueryable<Transaction> query,
            GetTransactionsQuery request)
        {
            if (request.Category.HasValue)
                query = query.Where(t => t.Category == request.Category.Value);

            if (request.Status.HasValue)
                query = query.Where(t => t.Status == request.Status.Value);

            if (request.FromDate.HasValue)
            {
                var from = DateTime.SpecifyKind(request.FromDate.Value, DateTimeKind.Utc);
                query = query.Where(t => t.Date >= from);
            }

            if (request.ToDate.HasValue)
            {
                var to = DateTime.SpecifyKind(request.ToDate.Value, DateTimeKind.Utc);
                query = query.Where(t => t.Date <= to);
            }

            if (request.MinAmount.HasValue)
                query = query.Where(t => Math.Abs(t.Amount.Amount) >= request.MinAmount.Value);

            if (request.MaxAmount.HasValue)
                query = query.Where(t => Math.Abs(t.Amount.Amount) <= request.MaxAmount.Value);

            if (!string.IsNullOrWhiteSpace(request.SearchTerm))
            {
                var searchTerm = request.SearchTerm.ToLower();
                query = query.Where(t =>
                    t.Description.ToLower().Contains(searchTerm) ||
                    t.Source.Name.ToLower().Contains(searchTerm));
            }

            if (!string.IsNullOrWhiteSpace(request.Source))
            {
                query = query.Where(t => t.Source.Name == request.Source);
            }

            return query;
        }

        private static IQueryable<Transaction> ApplySorting(
            IQueryable<Transaction> query,
            GetTransactionsQuery request)
        {
            if (string.IsNullOrWhiteSpace(request.SortBy))
                return request.SortDescending
                    ? query.OrderByDescending(t => t.Date)
                    : query.OrderBy(t => t.Date);

            return request.SortBy.ToLower() switch
            {
                "amount" => request.SortDescending
                    ? query.OrderByDescending(t => t.Amount.Amount)
                    : query.OrderBy(t => t.Amount.Amount),
                "date" => request.SortDescending
                    ? query.OrderByDescending(t => t.Date)
                    : query.OrderBy(t => t.Date),
                "category" => request.SortDescending
                    ? query.OrderByDescending(t => t.Category)
                    : query.OrderBy(t => t.Category),
                "status" => request.SortDescending
                    ? query.OrderByDescending(t => t.Status)
                    : query.OrderBy(t => t.Status),
                "description" => request.SortDescending
                    ? query.OrderByDescending(t => t.Description)
                    : query.OrderBy(t => t.Description),
                _ => request.SortDescending
                    ? query.OrderByDescending(t => t.Date)
                    : query.OrderBy(t => t.Date)
            };
        }
    }
}
