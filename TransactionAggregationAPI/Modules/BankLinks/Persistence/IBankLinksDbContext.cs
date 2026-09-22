using Microsoft.EntityFrameworkCore;

namespace Modules.BankLinks.Persistence
{
    public interface IBankLinksDbContext
    {
        DbSet<BankLink> BankLinks { get; }

        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
