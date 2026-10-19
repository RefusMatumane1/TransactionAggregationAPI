using BuildingBlocks.Application.Abstractions.Authentication;
using BuildingBlocks.Application.Querying;
using Microsoft.EntityFrameworkCore;
using Modules.Customers.Application.Persistence;
using Modules.Customers.Contracts;
using Modules.Customers.Domain.ValueObjects;

namespace Modules.Customers.Application.Contracts
{
    internal sealed class CustomerAccounts(ICustomersDbContext context) : ICustomerAccounts
    {
        public async Task<IReadOnlyList<LinkedAccountRef>?> FindAsync(Guid customerId, CancellationToken cancellationToken = default)
        {
            var id = CustomerId.CreateFrom(customerId);
            var customer = await context.Customers
                .AsNoTracking()
                .Where(c => c.Id == id)
                .Select(c => new { Accounts = c.Accounts.Select(a => new LinkedAccountRef(a.Institution, a.ExternalAccountId)).ToList() })
                .FirstOrDefaultAsync(cancellationToken);

            return customer?.Accounts;
        }

        public async Task<IReadOnlyList<AccountKey>?> FindVisibleAsync(
            Guid customerId, InstitutionAccess access, CancellationToken cancellationToken = default)
        {
            var linked = await FindAsync(customerId, cancellationToken);
            if (linked is null)
                return null;

            var visible = linked
                .Where(a => access.AllInstitutions || access.Institutions.Contains(a.Institution, StringComparer.OrdinalIgnoreCase))
                .Select(a => new AccountKey(a.Institution, a.ExternalAccountId))
                .OrderBy(a => a.Institution, StringComparer.Ordinal)
                .ThenBy(a => a.ExternalAccountId, StringComparer.Ordinal)
                .ToList();

            return visible.Count == 0 && !access.AllInstitutions ? null : visible;
        }
    }
}