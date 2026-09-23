using Microsoft.EntityFrameworkCore;
using Modules.Customers.Domain;

namespace Modules.Customers.Application.Persistence
{
    public interface ICustomersDbContext
    {
        DbSet<Customer> Customers { get; }
        DbSet<Account> Accounts { get; }

        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
