using System;

namespace TransactionAggregation.Application.Common.DTOs
{
    /// <summary>A single transaction as pushed to us by the account aggregator's webhook — see
    /// ReceiveBankTransactionsCommand. The owning BankLink (looked up by ExternalAccountId)
    /// supplies which internal Account/institution it belongs to, so this DTO only carries the
    /// transaction's own data.</summary>
    public record ExternalTransactionDTO
    {
        public string Id { get; init; } = null!;
        public decimal Amount { get; init; }
        public string Currency { get; init; } = null!;
        public string Description { get; init; } = null!;
        public string Category { get; init; } = null!;
        public DateTime Date { get; init; }
    }
}
