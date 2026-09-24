using Microsoft.EntityFrameworkCore;
using Modules.BankLinks.Application.Persistence;
using Modules.BankLinks.Contracts;
using Modules.BankLinks.Domain.ValueObjects;

namespace Modules.BankLinks.Application.Contracts
{
    internal sealed class BankLinksReadApi(IBankLinksDbContext context) : IBankLinksReadApi
    {
        public async Task<IReadOnlyList<ActiveBankLinkInfo>> FindActiveLinksByExternalAccountIdAsync(
            string externalAccountId,
            CancellationToken cancellationToken = default)
        {
            // Ordered so every caller sees the links in the same order on every call.
            var links = await context.BankLinks
                .AsNoTracking()
                .Where(b => b.ExternalAccountId == externalAccountId
                            && b.Status == BankLinkStatus.Active
                            && b.AccountId != null)
                .OrderBy(b => b.CreatedAt)
                .ThenBy(b => b.Id)
                .ToListAsync(cancellationToken);

            return links
                .Select(link => new ActiveBankLinkInfo(
                    link.Id.Value,
                    link.CustomerId.Value,
                    link.AccountId!.Value,
                    link.Institution.ToString()))
                .ToList();
        }
    }
}