using Microsoft.EntityFrameworkCore;
using Modules.Customers.Contracts;
using Modules.Transactions.Application.Common.Interfaces;
using Modules.Transactions.Domain.Enums;
using Modules.Transactions.Domain.Services;
using SharedKernel.Common.ValueObjects;

namespace Modules.Transactions.Application.Adapters
{
    /// <summary>
    /// Implements Customers' IAccountBalanceProvider. The balance is booked (Settled) transactions only;
    /// pending ones are reported separately (TransactionTotals).
    /// </summary>
    public sealed class TransactionBalanceProvider(ITransactionsDbContext _context) : IAccountBalanceProvider
    {
        public async Task<AccountBalance> GetBalanceAsync(Guid accountId, CancellationToken cancellationToken = default)
        {
            var accountIdVo = AccountId.CreateFrom(accountId);

            var rows = await _context.Transactions
                .AsNoTracking()
                .Where(t => t.AccountId == accountIdVo)
                .Select(t => new { t.Status, t.Amount.Amount })
                .ToListAsync(cancellationToken);

            return Summarise(rows.Select(r => (r.Status, r.Amount)));
        }

        public async Task<IReadOnlyDictionary<Guid, AccountBalance>> GetBalancesByCustomerAsync(
            Guid customerId,
            CancellationToken cancellationToken = default)
        {
            var customerIdVo = CustomerId.CreateFrom(customerId);

            var rows = await _context.Transactions
                .AsNoTracking()
                .Where(t => t.CustomerId == customerIdVo && t.AccountId != null)
                .Select(t => new { AccountId = t.AccountId!.Value, t.Status, t.Amount.Amount })
                .ToListAsync(cancellationToken);

            return rows
                .GroupBy(r => r.AccountId)
                .ToDictionary(g => g.Key, g => Summarise(g.Select(r => (r.Status, r.Amount))));
        }

        private static AccountBalance Summarise(IEnumerable<(TransactionStatus Status, decimal Amount)> rows)
        {
            decimal booked = 0, pendingDebits = 0, pendingCredits = 0;

            foreach (var (status, amount) in rows)
            {
                if (TransactionTotals.CountsAsBooked(status))
                    booked += amount;
                else if (TransactionTotals.CountsAsPending(status))
                {
                    if (amount < 0)
                        pendingDebits += amount;
                    else
                        pendingCredits += amount;
                }
            }

            return new AccountBalance(booked, pendingDebits, pendingCredits);
        }
    }
}