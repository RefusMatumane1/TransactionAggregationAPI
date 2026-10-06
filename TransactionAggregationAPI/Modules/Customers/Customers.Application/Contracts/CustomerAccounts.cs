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
    }
}