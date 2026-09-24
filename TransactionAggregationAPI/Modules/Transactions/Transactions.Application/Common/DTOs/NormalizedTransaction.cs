using Modules.Transactions.Domain.Enums;

namespace Modules.Transactions.Application.Common.DTOs
{
    /// <summary>
    /// A bank transaction in canonical form — the output of the normalization stage and the
    /// only shape the rest of ingestion (duplicate checks, settlement, categorization,
    /// storage) works with. Whatever bank it came from, the date is UTC, the currency is
    /// upper-case ISO 4217, the description is cleaned, and the bank's own category label is
    /// mapped to ours where possible.
    /// </summary>
    /// <param name="ExternalId">The bank's id, exactly as sent — ids are opaque and never normalized.</param>
    /// <param name="Amount">Negative is money out, positive is money in.</param>
    /// <param name="BankCategory">The bank's category mapped to ours, or null when it sent none or one we don't recognise.</param>
    /// <param name="OriginalDescription">The description as the bank sent it, when cleaning changed it.</param>
    /// <param name="OriginalCategory">The bank's category label as sent, when it sent one.</param>
    public sealed record NormalizedTransaction(
        string ExternalId,
        decimal Amount,
        string Currency,
        string Description,
        DateTime DateUtc,
        bool IsPending,
        TransactionCategory? BankCategory,
        string? OriginalDescription,
        string? OriginalCategory);

    /// <summary>Transaction metadata keys that preserve what the bank originally sent.</summary>
    public static class BankProvenanceMetadata
    {
        public const string Description = "bankDescription";
        public const string Category = "bankCategory";
    }
}