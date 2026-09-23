using Microsoft.EntityFrameworkCore;
using SharedKernel.Common.ValueObjects;
using Modules.Customers.Contracts;
using Modules.Transactions.Application.Common.Interfaces;

namespace Modules.Transactions.Application.Adapters
{
    /// <summary>
    /// Implements the port Customers owns. Lives here (not in Customers) because it needs
    /// direct access to the Transaction entity and ITransactionsDbContext — neither of
    /// which Customers should depend on. Sums the same way
    /// GetAccountByIdQueryHandler/GetCustomerAccountsQueryHandler used to before the
    /// Customers extraction. Wired to IAccountBalanceProvider in Program.cs.
    /// </summary>
    public sealed class TransactionBalanceProvider(ITransactionsDbContext _context) : IAccountBalanceProvider
    {
        public async Task<decimal> GetBalanceAsync(Guid accountId, CancellationToken cancellationToken = default)
        {
            var accountIdVo = AccountId.CreateFrom(accountId);

            return await _context.Transactions
                .AsNoTracking()
                .Where(t => t.AccountId == accountIdVo)
                .SumAsync(t => t.Amount.Amount, cancellationToken);
        }

        public async Task<IReadOnlyDictionary<Guid, decimal>> GetBalancesByCustomerAsync(
            Guid customerId,
            CancellationToken cancellationToken = default)
        {
            var customerIdVo = CustomerId.CreateFrom(customerId);

            var balances = (await _context.Transactions
                .AsNoTracking()
                .Where(t => t.CustomerId == customerIdVo && t.AccountId != null)
                .Select(t => new { AccountId = t.AccountId!.Value, t.Amount.Amount })
                .ToListAsync(cancellationToken))
                .GroupBy(t => t.AccountId)
                .ToDictionary(g => g.Key, g => g.Sum(t => t.Amount));

            return balances;
        }
    }
}
