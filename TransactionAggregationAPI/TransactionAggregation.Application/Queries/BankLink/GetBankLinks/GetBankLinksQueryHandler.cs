using Microsoft.EntityFrameworkCore;
using TransactionAggregation.Application.Abstractions;
using TransactionAggregation.Application.Common.DTOs;
using TransactionAggregation.Application.Common.Interfaces;
using TransactionAggregation.Application.Common.Models;
using TransactionAggregation.Domain.Common.ValueObjects;

namespace TransactionAggregation.Application.Queries.BankLink.GetBankLinks
{
    internal sealed class GetBankLinksQueryHandler(IApplicationDbContext _context)
        : IQueryHandler<GetBankLinksQuery, IReadOnlyList<BankLinkDto>>
    {
        public async Task<Result<IReadOnlyList<BankLinkDto>>> Handle(GetBankLinksQuery request, CancellationToken cancellationToken)
        {
            var customerId = CustomerId.CreateFrom(request.CustomerId);

            var links = await _context.BankLinks
                .AsNoTracking()
                .Where(b => b.CustomerId == customerId)
                .ToListAsync(cancellationToken);

            // Projected in-memory rather than in the query itself — EF Core can't translate
            // property access on a converted value object (b.Id.Value) into SQL.
            var dtos = links
                .Select(b => new BankLinkDto(
                    b.Id.Value,
                    b.Institution,
                    b.Status,
                    b.AccountId?.Value,
                    b.CreatedAt))
                .ToList();

            return Result.Success<IReadOnlyList<BankLinkDto>>(dtos);
        }
    }
}
