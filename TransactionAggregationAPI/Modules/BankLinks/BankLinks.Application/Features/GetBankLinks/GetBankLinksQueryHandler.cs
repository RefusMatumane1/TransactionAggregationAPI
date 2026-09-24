using Microsoft.EntityFrameworkCore;
using Modules.BankLinks.Application.DTOs;
using Modules.BankLinks.Application.Persistence;
using SharedKernel.Abstractions;
using SharedKernel.Common.Models;
using SharedKernel.Common.ValueObjects;

namespace Modules.BankLinks.Application.Features.GetBankLinks
{
    internal sealed class GetBankLinksQueryHandler(IBankLinksDbContext _context)
        : IQueryHandler<GetBankLinksQuery, IReadOnlyList<BankLinkDto>>
    {
        public async Task<Result<IReadOnlyList<BankLinkDto>>> Handle(GetBankLinksQuery request, CancellationToken cancellationToken)
        {
            var customerId = CustomerId.CreateFrom(request.CustomerId);

            var links = await _context.BankLinks
                .AsNoTracking()
                .Where(b => b.CustomerId == customerId)
                .ToListAsync(cancellationToken);

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