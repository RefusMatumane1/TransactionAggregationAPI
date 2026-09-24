using MediatR;
using Modules.Transactions.Application.Common.Models;
using Modules.Transactions.Domain.Enums;
using SharedKernel.Common.Models;

namespace Modules.Transactions.Application.Features.Transactions.Queries.ExportTransactions
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