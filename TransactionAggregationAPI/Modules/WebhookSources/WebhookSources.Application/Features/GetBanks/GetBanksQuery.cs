using BuildingBlocks.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Modules.WebhookSources.Application.DTOs;
using Modules.WebhookSources.Application.Persistence;
using SharedKernel.Common.Models;

namespace Modules.WebhookSources.Application.Features.GetBanks
{
    // Every bank, active or not, so the UI can label and colour any transaction it shows. The list
    // is a small reference set (one row per bank), so it is returned whole rather than paged.
    public sealed record GetBanksQuery : IQuery<IReadOnlyList<BankDto>>;

    internal sealed class GetBanksQueryHandler(IWebhookSourcesDbContext context)
        : IQueryHandler<GetBanksQuery, IReadOnlyList<BankDto>>
    {
        public const int MaxBanks = 500;

        public async Task<Result<IReadOnlyList<BankDto>>> Handle(GetBanksQuery request, CancellationToken cancellationToken)
        {
            var banks = await context.WebhookSources
                .AsNoTracking()
                .OrderBy(s => s.DisplayName)
                .Take(MaxBanks)
                .Select(s => new BankDto(s.Name, s.DisplayName, s.Color, s.IsActive, s.LastUsedAt))
                .ToListAsync(cancellationToken);

            return Result.Success<IReadOnlyList<BankDto>>(banks);
        }
    }
}