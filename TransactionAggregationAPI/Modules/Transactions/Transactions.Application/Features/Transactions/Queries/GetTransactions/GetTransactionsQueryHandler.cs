using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Application.Pagination;
using Microsoft.EntityFrameworkCore;
using Modules.Currency.Contracts;
using Modules.Transactions.Application.Common.DTOs;
using Modules.Transactions.Application.Common.Interfaces;
using Modules.Transactions.Application.Common.Pagination;
using Modules.Transactions.Application.Common.Queries;
using Modules.Transactions.Domain.Entities;
using SharedKernel.Common.Models;

namespace Modules.Transactions.Application.Features.Transactions.Queries.GetTransactions
{
    internal sealed class GetTransactionsQueryHandler(
        ITransactionsDbContext context, ITransactionSearch search, IKeysetPaginator paginator, ICurrencyCatalog catalog)
        : IQueryHandler<GetTransactionsQuery, CursorPage<TransactionListItemDto>>
    {
        public async Task<Result<CursorPage<TransactionListItemDto>>> Handle(
            GetTransactionsQuery request,
            CancellationToken cancellationToken)
        {
            if (request.ConvertTo is not null && !catalog.IsSupported(request.ConvertTo))
                return Result.Failure<CursorPage<TransactionListItemDto>>(Error.Validation(
                    $"convertTo must be one of the supported currencies: {string.Join(", ", catalog.Supported)}."));

            var filtered = ApplyFilters(context.Transactions.Ledger(request.Filter), request);

            BoundedCount? total = request.IncludeTotal
                ? await BoundedCount.OfAsync(filtered, (q, ct) => q.CountAsync(ct), cancellationToken)
                : null;

            var sort = TransactionSorts.Resolve(request.SortBy);
            var window = PageCursor.TryDecode(request.Cursor, out var cursor)
                ? sort.After(filtered, paginator, cursor!)
                : filtered;

            var fetched = await sort.Order(window, request.SortDescending)
                .Take(request.PageSize + 1)
                .ToListAsync(cancellationToken);

            return Result.Success(sort.ToPage(
                fetched, request.PageSize, request.SortDescending,
                t => TransactionListItemDto.From(t).Converted(catalog, request.ConvertTo), total));
        }

        private IQueryable<Transaction> ApplyFilters(IQueryable<Transaction> query, GetTransactionsQuery request)
        {
            if (request.Category.HasValue)
                query = query.Where(t => t.Category == request.Category.Value);

            if (!string.IsNullOrEmpty(request.Currency))
                query = query.Where(t => t.Amount.Currency == request.Currency);

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
                query = search.DescriptionContains(query, request.SearchTerm.Trim());

            return query;
        }
    }
}