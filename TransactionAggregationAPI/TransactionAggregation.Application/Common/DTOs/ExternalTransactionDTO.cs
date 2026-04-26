using System;
using System.Collections.Generic;
using System.Text;

namespace TransactionAggregation.Application.Common.DTOs
{
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
