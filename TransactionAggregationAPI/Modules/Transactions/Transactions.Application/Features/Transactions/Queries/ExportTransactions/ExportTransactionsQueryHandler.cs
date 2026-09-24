using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Modules.Transactions.Application.Common.Interfaces;
using Modules.Transactions.Domain.Entities;
using SharedKernel.Common.Models;
using SharedKernel.Common.ValueObjects;
using System.Globalization;
using System.Text;

namespace Modules.Transactions.Application.Features.Transactions.Queries.ExportTransactions
{
    public sealed class ExportTransactionsQueryHandler : IRequestHandler<ExportTransactionsQuery, Result<ExportTransactionsResult>>
    {
        public const int MaxExportRows = 50_000;

        private static readonly Encoding Utf8Bom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);

        private readonly ITransactionsDbContext _context;
        private readonly ILogger<ExportTransactionsQueryHandler> _logger;
        private readonly int _maxExportRows;

        public ExportTransactionsQueryHandler(
            ITransactionsDbContext context,
            ILogger<ExportTransactionsQueryHandler> logger)
            : this(context, logger, MaxExportRows)
        {
        }

        /// <summary>Lets tests exercise the cap without materialising 50,000 rows.</summary>
        internal ExportTransactionsQueryHandler(
            ITransactionsDbContext context,
            ILogger<ExportTransactionsQueryHandler> logger,
            int maxExportRows)
        {
            _context = context;
            _logger = logger;
            _maxExportRows = maxExportRows;
        }

        public async Task<Result<ExportTransactionsResult>> Handle(
            ExportTransactionsQuery request,
            CancellationToken cancellationToken)
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

            // The export is built in memory, so its size is bounded up front: an unfiltered
            // export of a long-lived account would otherwise load its whole history into one
            // request. Above the cap the caller narrows the date range (or pages /filter).
            var matching = await query.CountAsync(cancellationToken);
            if (matching > _maxExportRows)
                return Result.Failure<ExportTransactionsResult>(new FieldValidationError(
                    new Dictionary<string, string[]>
                    {
                        ["fromDate"] = [$"{matching} transactions match; an export is limited to {_maxExportRows}. Narrow the date range."]
                    }));

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

        private static string GenerateCsv(List<Transaction> transactions)
        {
            var csv = new StringBuilder();

            csv.AppendLine("Date,Time,Description,Amount,Currency,Flow,Category,Status,Source,External ID");

            foreach (var t in transactions)
            {
                csv.AppendLine(string.Join(",",
                    t.Date.ToString("yyyy-MM-dd"),
                    t.Date.ToString("HH:mm:ss"),
                    Quote(t.Description),
                    // At least 2 decimals, up to the 4 stored.
                    t.Amount.Amount.ToString("0.00##", CultureInfo.InvariantCulture),
                    t.Amount.Currency,
                    t.IsIncome ? "Income" : "Expense",
                    t.Category.ToString(),
                    t.Status.ToString(),
                    Quote(t.Source.Name),
                    Quote(t.Source.ExternalId)
                ));
            }

            return csv.ToString();
        }

        /// <summary>
        /// Quotes a field and neutralises spreadsheet formulas: descriptions and ids come from
        /// external providers, and a cell starting with = + - @ (or a tab/CR) is executed by
        /// Excel/Sheets when the export is opened (OWASP "CSV injection"). A leading apostrophe
        /// makes the cell literal text.
        /// </summary>
        internal static string Quote(string value)
        {
            if (value.Length > 0 && FormulaTriggers.Contains(value[0]))
                value = "'" + value;

            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        private static readonly char[] FormulaTriggers = ['=', '+', '-', '@', '\t', '\r'];
    }
}