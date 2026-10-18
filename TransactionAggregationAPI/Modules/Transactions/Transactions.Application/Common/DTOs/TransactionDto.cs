using Modules.Transactions.Application.Common.Currency;
using Modules.Transactions.Domain.Entities;
using Modules.Transactions.Domain.Enums;

namespace Modules.Transactions.Application.Common.DTOs
{
    public sealed record TransactionDto(
        Guid Id,
        string Institution,
        string ExternalAccountId,
        string ExternalTransactionId,
        decimal Amount,
        string Currency,
        DateTime TransactionDate,
        string Description,
        TransactionCategory Category,
        DateTime RecordedAt,
        IReadOnlyDictionary<string, string> Metadata,
        decimal? ConvertedAmount = null,
        string? ConvertedCurrency = null)
    {
        public TransactionDto Converted(ICurrencyCatalog catalog, string? target) =>
            target is null ? this : this with { ConvertedAmount = catalog.Convert(Amount, Currency, target), ConvertedCurrency = target };

        public static TransactionDto From(Transaction transaction) => new(
            transaction.Id.Value,
            transaction.Source.Name,
            transaction.ExternalAccountId,
            transaction.Source.ExternalId,
            transaction.Amount.Amount,
            transaction.Amount.Currency,
            transaction.Date,
            transaction.Description,
            transaction.Category,
            transaction.CreatedAt,
            transaction.Metadata.ToDictionary());
    }
}