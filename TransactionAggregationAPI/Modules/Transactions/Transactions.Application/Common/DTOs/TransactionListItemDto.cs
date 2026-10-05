using Modules.Transactions.Domain.Entities;
using Modules.Transactions.Domain.Enums;

namespace Modules.Transactions.Application.Common.DTOs
{
    public sealed record TransactionListItemDto
    {
        public Guid Id { get; init; }
        public string ExternalAccountId { get; init; } = null!;
        public decimal Amount { get; init; }
        public string Currency { get; init; } = null!;
        public string Description { get; init; } = null!;
        public TransactionCategory Category { get; init; }
        public string Source { get; init; } = null!;
        public DateTime Date { get; init; }
        public DateTime CreatedAt { get; init; }
        public Dictionary<string, string> Metadata { get; init; } = new();

        public static TransactionListItemDto From(Transaction transaction) => new()
        {
            Id = transaction.Id.Value,
            ExternalAccountId = transaction.ExternalAccountId,
            Amount = transaction.Amount.Amount,
            Currency = transaction.Amount.Currency,
            Description = transaction.Description,
            Category = transaction.Category,
            Source = transaction.Source.Name,
            Date = transaction.Date,
            CreatedAt = transaction.CreatedAt,
            Metadata = transaction.Metadata.ToDictionary()
        };
    }
}