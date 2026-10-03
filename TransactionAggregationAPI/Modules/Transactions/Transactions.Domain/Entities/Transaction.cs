using Modules.Transactions.Domain.Common.ValueObjects;
using Modules.Transactions.Domain.Enums;
using SharedKernel.Exceptions;

namespace Modules.Transactions.Domain.Entities
{
    // A ledger entry: a transaction the bank has posted. It is recorded once and never changed or
    // deleted; the database rejects UPDATE and DELETE on its table as well.
    public sealed class Transaction
    {
        public const int MaxDescriptionLength = 500;
        public const int MaxExternalIdLength = TransactionSource.MaxExternalIdLength;
        public const int MaxExternalAccountIdLength = 200;

        private readonly Dictionary<string, string> _metadata = new();

        private Transaction() { }

        private Transaction(
            string externalAccountId,
            Money amount,
            string description,
            TransactionCategory category,
            TransactionSource source,
            DateTime bookedAt,
            IReadOnlyDictionary<string, string> metadata,
            DateTime recordedAt)
        {
            if (string.IsNullOrWhiteSpace(description) || description.Length > MaxDescriptionLength)
                throw new DomainException($"Description must be 1-{MaxDescriptionLength} characters");
            if (string.IsNullOrWhiteSpace(externalAccountId) || externalAccountId.Length > MaxExternalAccountIdLength)
                throw new DomainException($"External account ID must be 1-{MaxExternalAccountIdLength} characters");
            if (bookedAt.Kind != DateTimeKind.Utc)
                throw new DomainException("The booking date must be in UTC");

            Id = TransactionId.Create();
            ExternalAccountId = externalAccountId;
            Amount = amount;
            Description = description;
            Category = category;
            Source = source;
            Date = bookedAt;
            Status = TransactionStatus.Booked;
            CreatedAt = recordedAt;
            foreach (var (key, value) in metadata)
                _metadata[key] = value;
        }

        public TransactionId Id { get; private set; } = null!;

        // The bank account as the bank identifies it: unique only within its institution.
        public string ExternalAccountId { get; private set; } = null!;
        public Money Amount { get; private set; } = null!;
        public string Description { get; private set; } = null!;
        public TransactionCategory Category { get; private set; }

        // Institution (Name) and the bank's own transaction id (ExternalId).
        public TransactionSource Source { get; private set; } = null!;

        // When the bank booked it (UTC).
        public DateTime Date { get; private set; }
        public TransactionStatus Status { get; private set; }

        // When this platform recorded it (UTC).
        public DateTime CreatedAt { get; private set; }

        public IReadOnlyDictionary<string, string> Metadata => _metadata;

        public static Transaction Record(
            string externalAccountId,
            Money amount,
            string description,
            TransactionCategory category,
            TransactionSource source,
            DateTime bookedAt,
            IReadOnlyDictionary<string, string>? metadata = null,
            DateTime? recordedAt = null) =>
            new(externalAccountId, amount, description, category, source, bookedAt,
                metadata ?? new Dictionary<string, string>(), recordedAt ?? DateTime.UtcNow);

        public bool IsExpense => Amount.IsExpense;
        public bool IsIncome => Amount.IsIncome;
    }
}