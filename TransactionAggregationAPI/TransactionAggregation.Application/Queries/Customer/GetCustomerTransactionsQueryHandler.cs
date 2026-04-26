using Mapster;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using TransactionAggregation.Application.Abstractions;
using TransactionAggregation.Application.Common.Interfaces;
using TransactionAggregation.Application.Common.Models;
using TransactionAggregation.Application.Features.Transactions.DTOs;
using TransactionAggregation.Domain.Common.ValueObjects;

namespace TransactionAggregation.Application.Queries.Customer
{
    internal sealed class GetCustomerTransactionsQueryHandler
        : IQueryHandler<GetCustomerTransactionsQuery, PagedResult<TransactionDto>>
    {
        private readonly IApplicationDbContext _context;
        private readonly IMapper _mapper;

        public GetCustomerTransactionsQueryHandler(IApplicationDbContext context, IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
        }

        public async Task<Result<PagedResult<TransactionDto>>> Handle(
            GetCustomerTransactionsQuery request,
            CancellationToken cancellationToken)
        {
            var customerId = CustomerId.CreateFrom(request.CustomerId);

            var query = _context.Transactions
                .Where(t => t.CustomerId == customerId)
                .AsQueryable();

            if (request.StartDate.HasValue)
                query = query.Where(t => t.Date >= request.StartDate.Value);

            if (request.EndDate.HasValue)
                query = query.Where(t => t.Date <= request.EndDate.Value);

            if (request.Category.HasValue)
                query = query.Where(t => t.Category == request.Category.Value);

            var totalCount = await query.CountAsync(cancellationToken);

            var transactions = await query
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize)
                .ProjectToType<TransactionDto>(_mapper.Config)
                .ToListAsync(cancellationToken);

            return Result<PagedResult<TransactionDto>>.Success(
                new PagedResult<TransactionDto>(transactions, totalCount, request.Page, request.PageSize));
        }
    }

}
