using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Application.Abstractions.Authentication;
using Microsoft.EntityFrameworkCore;
using Modules.WebhookSources.Application.DTOs;
using Modules.WebhookSources.Application.Persistence;
using SharedKernel.Common.Models;

namespace Modules.WebhookSources.Application.Features.GetBanks
{
    // Staff get only their assigned banks. Returned whole: it is a small reference set.
    public sealed record GetBanksQuery(InstitutionAccess Access) : IQuery<IReadOnlyList<BankDto>>;

    internal sealed class GetBanksQueryHandler(IWebhookSourcesDbContext context)
        : IQueryHandler<GetBanksQuery, IReadOnlyList<BankDto>>
    {
        public const int MaxBanks = 500;

        public async Task<Result<IReadOnlyList<BankDto>>> Handle(GetBanksQuery request, CancellationToken cancellationToken)
        {
            var query = context.WebhookSources.AsNoTracking();
            if (!request.Access.AllInstitutions)
            {
                var institutions = request.Access.Institutions.ToList();
                query = query.Where(s => institutions.Contains(s.Name));
            }

            var banks = await query
                .OrderBy(s => s.DisplayName)
                .Take(MaxBanks)
                .Select(s => new BankDto(s.Name, s.DisplayName, s.Color, s.IsActive, s.LastUsedAt))
                .ToListAsync(cancellationToken);

            return Result.Success<IReadOnlyList<BankDto>>(banks);
        }
    }
}