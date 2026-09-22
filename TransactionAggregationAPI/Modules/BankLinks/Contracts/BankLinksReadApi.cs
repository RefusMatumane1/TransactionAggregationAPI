using Microsoft.EntityFrameworkCore;
using Modules.BankLinks.Persistence;
using Modules.BankLinks.ValueObjects;

namespace Modules.BankLinks.Contracts
{
    internal sealed class BankLinksReadApi(IBankLinksDbContext context) : IBankLinksReadApi
    {
        public async Task<ActiveBankLinkInfo?> FindActiveLinkByExternalAccountIdAsync(
            string externalAccountId,
            CancellationToken cancellationToken = default)
        {
            var link = await context.BankLinks
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    b => b.ExternalAccountId == externalAccountId && b.Status == BankLinkStatus.Active,
                    cancellationToken);

            if (link is null || link.AccountId is null)
                return null;

            return new ActiveBankLinkInfo(
                link.Id.Value,
                link.CustomerId.Value,
                link.AccountId.Value,
                link.Institution.ToString());
        }
    }
}
