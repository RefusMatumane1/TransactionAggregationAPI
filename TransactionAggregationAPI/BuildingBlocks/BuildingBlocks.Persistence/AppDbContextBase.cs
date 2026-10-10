using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SharedKernel.Common;
using System.Data.Common;

namespace BuildingBlocks.Persistence
{
    public abstract class AppDbContextBase : DbContext
    {
        protected AppDbContextBase(DbContextOptions options)
            : base(options)
        {
        }

        public override async Task<int> SaveChangesAsync(
            bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        {
            foreach (var entry in ChangeTracker.Entries<BaseEntity>())
            {
                switch (entry.State)
                {
                    case EntityState.Added:
                        entry.Entity.CreatedAt = DateTime.UtcNow;
                        break;
                    case EntityState.Modified:
                        entry.Entity.UpdatedAt = DateTime.UtcNow;
                        break;
                }
            }

            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        protected async Task<int> SaveAtomicallyAsync(
            Func<DbTransaction?, CancellationToken, Task> enlist, CancellationToken cancellationToken)
        {
            if (!Database.IsRelational())
            {
                var saved = await SaveChangesAsync(acceptAllChangesOnSuccess: true, cancellationToken);
                await enlist(null, cancellationToken);
                return saved;
            }

            var strategy = Database.CreateExecutionStrategy();
            var result = await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await Database.BeginTransactionAsync(cancellationToken);
                var saved = await SaveChangesAsync(acceptAllChangesOnSuccess: false, cancellationToken);
                await enlist(transaction.GetDbTransaction(), cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return saved;
            });

            ChangeTracker.AcceptAllChanges();
            return result;
        }
    }
}