using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Text;
using TransactionAggregation.Application.Common.Interfaces;
using TransactionAggregation.Application.Common.Models;
using TransactionAggregation.Domain.Common.ValueObjects;
using TransactionAggregation.Domain.Entities;

namespace TransactionAggregation.Application.Features.Transactions.Queries.ExportTransactions
{
    public sealed class ExportTransactionsQueryHandler : IRequestHandler<ExportTransactionsQuery, Result<ExportTransactionsResult>>
    {
        private static readonly Encoding Utf8Bom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);

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

                if (request.FromDate.HasValue)
                {
                    var from = DateTime.SpecifyKind(request.FromDate.Value, DateTimeKind.Utc);
                    query = query.Where(t => t.Date >= from);
                }

                if (request.ToDate.HasValue)
                {
                    var to = DateTime.SpecifyKind(request.ToDate.Value, DateTimeKind.Utc);
                    query = query.Where(t => t.Date <= to);
                }

                if (request.Category.HasValue)
                    query = query.Where(t => t.Category == request.Category.Value);

                var transactions = await query
                    .OrderByDescending(t => t.Date)
                    .ToListAsync(cancellationToken);

                var content = GenerateCsv(transactions);

                var result = new ExportTransactionsResult
                {
                    Content = Utf8Bom.GetBytes(content),
                    ContentType = "text/csv; charset=utf-8",
                    FileName = $"transactions_{request.CustomerId}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv",
                    RecordCount = transactions.Count
                };

                _logger.LogInformation(
                    "Exported {RecordCount} transactions for customer {CustomerId}",
                    result.RecordCount, request.CustomerId);

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

            csv.AppendLine("Date,Time,Description,Amount,Currency,Flow,Category,Status,Source,External ID,Approved By");

            foreach (var t in transactions)
            {
                csv.AppendLine(string.Join(",",
                    t.Date.ToString("yyyy-MM-dd"),
                    t.Date.ToString("HH:mm:ss"),
                    Quote(t.Description),
                    t.Amount.Amount.ToString("F2", CultureInfo.InvariantCulture),
                    t.Amount.Currency,
                    t.IsIncome ? "Income" : "Expense",
                    t.Category.ToString(),
                    t.Status.ToString(),
                    Quote(t.Source.Name),
                    Quote(t.Source.ExternalId),
                    Quote(t.ApprovedBy ?? string.Empty)
                ));
            }

            return csv.ToString();
        }

        // Always wraps in quotes and escapes embedded quotes by doubling them (RFC 4180)
        private static string Quote(string value) =>
            $"\"{value.Replace("\"", "\"\"")}\"";
    }
}
