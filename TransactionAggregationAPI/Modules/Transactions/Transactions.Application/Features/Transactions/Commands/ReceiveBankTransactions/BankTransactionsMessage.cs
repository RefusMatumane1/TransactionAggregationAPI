using Modules.Transactions.Application.Common.DTOs;

namespace Modules.Transactions.Application.Features.Transactions.Commands.ReceiveBankTransactions
{
    public sealed record BankTransactionsMessage(
        string ExternalAccountId,
        string? Institution,
        IReadOnlyList<BankTransactionMessageItem> Transactions,
        int? SchemaVersion = null)
    {
        // v2 adds an optional Institution, which must name the source's bank.
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