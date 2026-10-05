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

        // Saves this context's changes and whatever `enlist` writes in one database transaction, so a
        // business change, the messages it causes and the audit rows describing it commit together or
        // not at all. `enlist` receives the open transaction and must write through the same
        // connection. The whole unit is retried by the execution strategy on a transient failure, so
        // `enlist` must be repeatable (save with acceptAllChangesOnSuccess: false).
        //
        // Without a relational provider (unit tests on the in-memory store) there is no transaction:
        // the changes are saved and `enlist` runs with a null transaction.
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