using MediatR;
using TransactionAggregation.Application.Common.Models;
using TransactionAggregation.Domain.Enums;

namespace TransactionAggregation.Application.Features.Transactions.Queries.ExportTransactions
{
    public sealed record ExportTransactionsQuery : IRequest<Result<ExportTransactionsResult>>
    {
        public required Guid CustomerId { get; init; }
        public DateTime? FromDate { get; init; }
        public DateTime? ToDate { get; init; }
        public TransactionCategory? Category { get; init; }
        public string? Format { get; init; } = "csv";
    }
    public sealed record ExportTransactionsResult
    {
        public byte[] Content { get; init; } = Array.Empty<byte>();
        public string ContentType { get; init; } = "text/csv";
        public string FileName { get; init; } = null!;
        public int RecordCount { get; init; }
    }
}
