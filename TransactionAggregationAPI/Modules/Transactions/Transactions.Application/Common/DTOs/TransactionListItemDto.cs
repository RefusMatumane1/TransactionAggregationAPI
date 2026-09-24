using Modules.Transactions.Domain.Enums;

namespace Modules.Transactions.Application.Common.DTOs
{
    /// <summary>
    /// One row of the filterable transaction list (GetTransactionsQuery). Data only: display
    /// formatting — amount text, relative age, enum names — is the API's concern and is built
    /// by the Presentation response, not here.
    /// </summary>
    public sealed record TransactionListItemDto
    {
        public Guid Id { get; init; }
        public Guid CustomerId { get; init; }
        public Guid? AccountId { get; init; }
        public decimal Amount { get; init; }
        public string Currency { get; init; } = null!;
        public string Description { get; init; } = null!;
        public TransactionCategory Category { get; init; }
        public TransactionStatus Status { get; init; }
        public string Source { get; init; } = null!;
        public DateTime Date { get; init; }
        public DateTime CreatedAt { get; init; }
        public Dictionary<string, string> Metadata { get; init; } = new();
    }
}