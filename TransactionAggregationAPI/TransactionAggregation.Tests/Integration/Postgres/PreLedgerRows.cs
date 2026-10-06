using Microsoft.EntityFrameworkCore;
using Modules.Transactions.Domain.Enums;

namespace TransactionAggregation.Tests.Integration.Postgres
{
    // Pending/Expired rows the domain can no longer create, seeded with SQL to prove reads leave them out.
    internal static class PreLedgerRows
    {
        public static Task<int> InsertAsync(
            DbContext context, string institution, string account, string externalId, decimal amount,
            TransactionStatus status, DateTime date, TransactionCategory category = TransactionCategory.Uncategorized) =>
            context.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO transactions."Transactions"
                    ("Id", "ExternalAccountId", "Amount", "Currency", "Description", "Category",
                     "SourceName", "SourceExternalId", "Status", "Date", "CreatedAt", "Metadata")
                VALUES ({Guid.NewGuid()}, {account}, {amount}, 'ZAR', 'pre-ledger row', {(int)category},
                        {institution}, {externalId}, {(int)status}, {date}, now(), jsonb_build_object())
                """);
    }
}