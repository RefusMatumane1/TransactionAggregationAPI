using BuildingBlocks.Application.Abstractions.Authentication;
using Modules.Transactions.Application.Common.Aggregation;
using Modules.Transactions.Domain.Common.ValueObjects;
using Modules.Transactions.Domain.Entities;
using Modules.Transactions.Domain.Enums;

namespace TransactionAggregation.Tests.Helpers
{
    public static class TestTransactions
    {
        public const string Account = "acc-1";

        public static Transaction Create(
            decimal amount,
            string description = "Test transaction",
            TransactionCategory category = TransactionCategory.Uncategorized,
            string institution = TestInstitutions.FNB,
            string account = Account,
            string? externalId = null,
            DateTime? date = null,
            string currency = "ZAR",
            IReadOnlyDictionary<string, string>? metadata = null) =>
            Transaction.Record(account,
                Money.Create(amount, currency),
                description,
                category,
                TransactionSource.Create(institution, externalId ?? Guid.NewGuid().ToString("N")),
                date ?? DateTime.UtcNow,
                metadata);
    }

    public static class TestFilters
    {
        public static TransactionFilter All { get; } = new(InstitutionAccess.All);

        public static TransactionFilter For(string? institution = null, string? account = null) =>
            new(InstitutionAccess.All, institution, account);

        public static TransactionFilter Only(params string[] institutions) => new(InstitutionAccess.Only(institutions));
    }
}