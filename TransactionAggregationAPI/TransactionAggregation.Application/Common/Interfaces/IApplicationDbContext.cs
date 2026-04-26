
using Microsoft.EntityFrameworkCore;
using TransactionAggregation.Domain.Entities;

namespace TransactionAggregation.Application.Common.Interfaces
{
    public interface IApplicationDbContext
    {
        DbSet<Transaction> Transactions { get; }

        DbSet<Customer> Customers { get; }

        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
