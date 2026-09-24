using MapsterMapper;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Modules.Transactions.Application.Common.DTOs;
using Modules.Transactions.Application.Common.Interfaces;
using Modules.Transactions.Application.Common.Models;
using Modules.Transactions.Domain.Entities;
using SharedKernel.Common.Models;
using SharedKernel.Common.ValueObjects;

namespace Modules.Transactions.Application.Features.Transactions.Queries.GetTransactions
{
    public sealed class GetTransactionsQueryHandler : IRequestHandler<GetTransactionsQuery, Result<PaginatedResponse<TransactionListItemDto>>>
    {
        private readonly ITransactionsDbContext _context;
        private readonly IMapper _mapper;

        public GetTransactionsQueryHandler(ITransactionsDbContext context, IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
        }

        public async Task<Result<PaginatedResponse<TransactionListItemDto>>> Handle(
            GetTransactionsQuery request,
            CancellationToken cancellationToken)
        {
            var query = _context.Transactions
                .Where(t => t.CustomerId == CustomerId.CreateFrom(request.CustomerId))
                .AsNoTracking();

            query = ApplyFilters(query, request);

            query = ApplySorting(query, request);

            var totalCount = await query.CountAsync(cancellationToken);

            var items = await query
                                .Skip((request.PageNumber - 1) * request.PageSize)
                                .Take(request.PageSize)
                                .ToListAsync(cancellationToken);

            var dtos = _mapper.Map<List<TransactionListItemDto>>(items);

            var response = PaginatedResponse<TransactionListItemDto>.Create(
                dtos,
                totalCount,
                request.PageNumber,
                request.PageSize);

            return Result<PaginatedResponse<TransactionListItemDto>>.Success(response);
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