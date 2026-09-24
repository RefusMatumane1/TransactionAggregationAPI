using Modules.Transactions.Domain.Entities;
using Modules.Transactions.Domain.Enums;
using System.Linq.Expressions;

namespace Modules.Transactions.Domain.Services
{
    /// <summary>
    /// The one rule for which transactions count toward money figures — income, expenses,
    /// net, spending by category/month, and account balances. Every query that computes a
    /// total must use these instead of its own status check, so the same customer never
    /// sees different numbers on different screens.
    ///
    /// Booked = Settled: money the bank has actually posted. Pending authorisations are
    /// reported separately (they can still change amount or disappear), and every other
    /// status — Rejected, Cancelled, Refunded, Expired, and the review states Approved /
    /// Flagged / Disputed — counts toward neither.
    /// </summary>
    public static class TransactionTotals
    {
        public static readonly Expression<Func<Transaction, bool>> IsBooked =
            t => t.Status == TransactionStatus.Settled;

        public static readonly Expression<Func<Transaction, bool>> IsPending =
            t => t.Status == TransactionStatus.Pending;

        public static bool CountsAsBooked(TransactionStatus status) => status == TransactionStatus.Settled;

        public static bool CountsAsPending(TransactionStatus status) => status == TransactionStatus.Pending;
    }
}