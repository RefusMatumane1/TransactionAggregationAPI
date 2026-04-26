using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using TransactionAggregation.Application.Common.Interfaces;

namespace TransactionAggregation.Persistence
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly ApplicationDbContext _context;
        private IDbContextTransaction? _currentTransaction;

        public UnitOfWork(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
        {


            _currentTransaction ??= await _context.Database.BeginTransactionAsync(cancellationToken);

            if (_currentTransaction != null)
                return;
        }

        public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                if (_currentTransaction == null)
                    throw new InvalidOperationException("No active transaction exists.");

                var strategy = _context.Database.CreateExecutionStrategy();
                await strategy.ExecuteAsync(async () =>
                {
                    try
                    {
                        await _context.SaveChangesAsync(cancellationToken);

                        await _currentTransaction.CommitAsync(cancellationToken);
                    }
                    catch
                    {
                        await _currentTransaction.RollbackAsync(cancellationToken);
                        throw;
                    }
                    finally
                    {
                        await _currentTransaction.DisposeAsync();
                        _currentTransaction = null;
                    }
                });
                await _context.SaveChangesAsync(cancellationToken);
                await (_currentTransaction?.CommitAsync(cancellationToken) ?? Task.CompletedTask);
            }
            finally
            {
                if (_currentTransaction != null)
                {
                    await _currentTransaction.DisposeAsync();
                    _currentTransaction = null;
                }
            }
        }

        public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                await (_currentTransaction?.RollbackAsync(cancellationToken) ?? Task.CompletedTask);
            }
            finally
            {
                if (_currentTransaction != null)
                {
                    await _currentTransaction.DisposeAsync();
                    _currentTransaction = null;
                }
            }
        }

        public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
