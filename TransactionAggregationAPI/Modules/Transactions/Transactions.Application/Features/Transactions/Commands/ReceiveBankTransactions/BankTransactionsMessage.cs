using Modules.Transactions.Application.Common.DTOs;

namespace Modules.Transactions.Application.Features.Transactions.Commands.ReceiveBankTransactions
{
    public sealed record BankTransactionsMessage(
        string ExternalAccountId,
        string? Institution,
        IReadOnlyList<BankTransactionMessageItem> Transactions,
        int? SchemaVersion = null)
    {
        // The bank is the source the delivery authenticated as. v2 added an optional
        // Institution field, which must then name that same bank; v1 has no such field.
        public const int CurrentSchemaVersion = 2;

        public static readonly IReadOnlySet<int> SupportedSchemaVersions = new HashSet<int> { 1, 2 };

        public int EffectiveSchemaVersion => SchemaVersion ?? CurrentSchemaVersion;

        public IReadOnlyList<ExternalTransactionDTO> ToExternalTransactionDtos() =>
            (Transactions ?? [])
                .Select(t => new ExternalTransactionDTO
                {
                    Id = t.Id,
                    Amount = t.Amount,
                    Currency = t.Currency,
                    Description = t.Description,
                    Category = t.Category ?? string.Empty,
                    Date = t.Date,
                    Status = t.Status
                })
                .ToList();
    }

    public sealed record BankTransactionMessageItem(
        string Id,
        decimal Amount,
        string Currency,
        string Description,
        string? Category,
        DateTime Date,
        string? Status = null);
}