using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Application.Pagination;
using Microsoft.EntityFrameworkCore;
using Modules.Customers.Application.Common;
using Modules.Customers.Application.DTOs;
using Modules.Customers.Application.Persistence;
using SharedKernel.Common.Models;

namespace Modules.Customers.Application.Features.GetCustomers
{
    internal sealed class GetCustomersQueryHandler(ICustomersDbContext context, IKeysetPaginator paginator)
        : IQueryHandler<GetCustomersQuery, CursorPage<CustomerSummaryDto>>
    {
        public async Task<Result<CursorPage<CustomerSummaryDto>>> Handle(GetCustomersQuery request, CancellationToken cancellationToken)
        {
            var query = context.Customers.AsNoTracking();

            // Staff see a customer only through an account at one of their banks.
            if (!request.Access.AllInstitutions)
            {
                var institutions = request.Access.Institutions.ToList();
                query = query.Where(c => c.Accounts.Any(a => institutions.Contains(a.Institution)));
            }

            if (!string.IsNullOrWhiteSpace(request.Search))
            {
                var term = request.Search.Trim().ToLowerInvariant();
                query = query.Where(c => c.Name.ToLower().Contains(term) || c.Reference.ToLower().Contains(term));
            }

            var sort = CustomerSort.ByName;
            var window = PageCursor.TryDecode(request.Cursor, out var cursor)
                ? sort.After(query, paginator, cursor!)
                : query;

            var customers = await sort.Order(window, descending: false)
                .Take(request.PageSize + 1)
                .ToListAsync(cancellationToken);

            return Result.Success(sort.ToPage(customers, request.PageSize, descending: false, c =>
            {
                var visible = c.Accounts.Where(a => request.Access.Includes(a.Institution)).ToList();
                return new CustomerSummaryDto(
                    c.Id.Value,
                    c.Reference,
                    c.Name,
                    visible.Count,
                    visible.Select(a => a.Institution).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.Ordinal).ToList());
            }));
        }
    }
}