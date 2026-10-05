using Microsoft.EntityFrameworkCore;

namespace TransactionAggregation.Tests.Integration.Postgres
{
    // Inserts rows in the pre-RemoveCustomerOwnership shape (CustomerId/AccountId columns). The
    // current EF model can't write that shape, so tests of older migrations seed with raw SQL.
    internal static class LegacyTransactionRows
    {
        public static Task<int> InsertAsync(
            DbContext context, Guid id, string externalId, decimal amount, int status,
            string description = "legacy", string institution = "FNB", Guid? customerId = null, Guid? accountId = null) =>
            context.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO transactions."Transactions"
                    ("Id", "CustomerId", "AccountId", "Amount", "Currency", "Description", "Category",
                     "SourceName", "SourceExternalId", "Status", "Date", "CreatedAt", "Metadata")
                VALUES ({id}, {customerId ?? Guid.NewGuid()}, {accountId}, {amount}, 'ZAR', {description}, 0,
                        {institution}, {externalId}, {status}, now(), now(), jsonb_build_object())
                """);
    }
}