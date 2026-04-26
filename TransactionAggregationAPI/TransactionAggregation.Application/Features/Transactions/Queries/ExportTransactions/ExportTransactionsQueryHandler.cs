using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text;
using TransactionAggregation.Application.Common.Interfaces;
using TransactionAggregation.Application.Common.Models;
using TransactionAggregation.Domain.Common.ValueObjects;
using TransactionAggregation.Domain.Entities;

namespace TransactionAggregation.Application.Features.Transactions.Queries.ExportTransactions
{
    public sealed class ExportTransactionsQueryHandler : IRequestHandler<ExportTransactionsQuery, Result<ExportTransactionsResult>>
    {
        private readonly IApplicationDbContext _context;
        private readonly ILogger<ExportTransactionsQueryHandler> _logger;

        public ExportTransactionsQueryHandler(
            IApplicationDbContext context,
            ILogger<ExportTransactionsQueryHandler> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<Result<ExportTransactionsResult>> Handle(
            ExportTransactionsQuery request,
            CancellationToken cancellationToken)
        {
            try
            {
                var query = _context.Transactions
                    .Where(t => t.CustomerId == CustomerId.CreateFrom(request.CustomerId))
                    .AsNoTracking();

                // Apply filters
                if (request.FromDate.HasValue)
                    query = query.Where(t => t.Date >= request.FromDate.Value);

                if (request.ToDate.HasValue)
                    query = query.Where(t => t.Date <= request.ToDate.Value);

                if (request.Category.HasValue)
                    query = query.Where(t => t.Category == request.Category.Value);

                var transactions = await query
                    .OrderByDescending(t => t.Date)
                    .ToListAsync(cancellationToken);

                var content = GenerateCsv(transactions);

                var result = new ExportTransactionsResult
                {
                    Content = Encoding.UTF8.GetBytes(content),
                    ContentType = "text/csv",
                    FileName = $"transactions_{request.CustomerId}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv",
                    RecordCount = transactions.Count
                };

                return Result.Success(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting transactions for customer {CustomerId}", request.CustomerId);
                return Result.Failure<ExportTransactionsResult>(
                    Error.Failure("ExportFailed", $"Failed to export transactions: {ex.Message}"));
            }
        }

        private static string GenerateCsv(List<Transaction> transactions)
        {
            var csv = new StringBuilder();

            // Header
            csv.AppendLine("Date,Description,Amount,Currency,Category,Status,Source,Notes");

            // Data
            foreach (var t in transactions)
            {
                csv.AppendLine($"{t.CreatedAt:yyyy-MM-dd HH:mm:ss}," +
                              $"{EscapeCsvField(t.Description)}," +
                              $"{t.Amount.Amount}," +
                              $"{t.Amount.Currency}," +
                              $"{t.Category}," +
                              $"{t.Status}," +
                              $"{t.Source.Name},");
            }

            return csv.ToString();
        }

        private static string EscapeCsvField(string field)
        {
            if (field.Contains(',') || field.Contains('"') || field.Contains('\n'))
                return $"\"{field.Replace("\"", "\"\"")}\"";
            return field;
        }
    }
}
