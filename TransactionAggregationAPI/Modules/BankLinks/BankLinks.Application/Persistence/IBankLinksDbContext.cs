using Microsoft.EntityFrameworkCore;
using Modules.BankLinks.Domain;

namespace Modules.BankLinks.Application.Persistence
{
    public interface IBankLinksDbContext
    {
        DbSet<BankLink> BankLinks { get; }

        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
