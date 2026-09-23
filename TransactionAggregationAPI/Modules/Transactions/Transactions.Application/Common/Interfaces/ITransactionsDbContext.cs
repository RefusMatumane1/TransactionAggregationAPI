
using Microsoft.EntityFrameworkCore;
using Modules.Transactions.Domain.Entities;

namespace Modules.Transactions.Application.Common.Interfaces
{
    public interface ITransactionsDbContext
    {
        DbSet<Transaction> Transactions { get; }

        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
