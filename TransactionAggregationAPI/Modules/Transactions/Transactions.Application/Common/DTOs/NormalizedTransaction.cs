using Modules.Transactions.Domain.Enums;

namespace Modules.Transactions.Application.Common.DTOs
{
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

    public static class BankProvenanceMetadata
    {
        public const string Description = "bankDescription";
        public const string Category = "bankCategory";
    }
}