using Modules.Transactions.Domain.Entities;

namespace TransactionAggregation.Tests.Helpers;

public static class TransactionTestExtensions
{
    /// <summary>
    /// Transaction.Create starts every transaction Pending, and only booked (Settled)
    /// transactions count toward totals and balances — tests about summing money settle first.
    /// </summary>
    public static Transaction Settled(this Transaction transaction)
    {
        transaction.Settle();
        return transaction;
    }
}