using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TransactionAggregation.Application.Abstractions;
using TransactionAggregation.Application.Common.DTOs;
using TransactionAggregation.Application.Common.Interfaces;
using TransactionAggregation.Application.Common.Models;
using TransactionAggregation.Domain.Common.ValueObjects;

namespace TransactionAggregation.Application.Queries.Customer
{
    internal sealed class GetTransactionSummaryQueryHandler(
        IApplicationDbContext _context,
        ILogger<GetTransactionSummaryQueryHandler> _logger)
        : IQueryHandler<GetTransactionSummaryQuery, TransactionSummaryDto>
    {
        public async Task<Result<TransactionSummaryDto>> Handle(
            GetTransactionSummaryQuery request,
            CancellationToken cancellationToken)
        {
            try
            {
                var customerId = CustomerId.CreateFrom(request.CustomerId);
                var startDate  = DateTime.SpecifyKind(request.StartDate, DateTimeKind.Utc);
                var endDate    = DateTime.SpecifyKind(request.EndDate,   DateTimeKind.Utc);

                var transactions = await _context.Transactions
                    .Where(t =>
                        t.CustomerId == customerId &&
                        t.Date >= startDate &&
                        t.Date <= endDate)
                    .AsNoTracking()
                    .ToListAsync(cancellationToken);

                if (!transactions.Any())
                {
                    return Result.Success(new TransactionSummaryDto(
                        TotalIncome: 0,
                        TotalExpenses: 0,
                        NetBalance: 0,
                        SpendingByCategory: new Dictionary<Domain.Enums.TransactionCategory, decimal>(),
                        TotalTransactions: 0,
                        MonthlySummaries: Array.Empty<MonthlySummaryDto>()));
                }

                var totalIncome = transactions
                    .Where(t => t.Amount.Amount > 0)
                    .Sum(t => t.Amount.Amount);

                var totalExpenses = transactions
                    .Where(t => t.Amount.Amount < 0)
                    .Sum(t => Math.Abs(t.Amount.Amount));

                var spendingByCategory = transactions
                    .Where(t => t.Amount.Amount < 0)
                    .GroupBy(t => t.Category)
                    .ToDictionary(
                        g => g.Key,
                        g => g.Sum(t => Math.Abs(t.Amount.Amount)));

                var monthlySummaries = transactions
                    .GroupBy(t => new { t.Date.Year, t.Date.Month })
                    .OrderBy(g => g.Key.Year)
                    .ThenBy(g => g.Key.Month)
                    .Select(g =>
                    {
                        var income = g.Where(t => t.Amount.Amount > 0).Sum(t => t.Amount.Amount);
                        var expenses = g.Where(t => t.Amount.Amount < 0).Sum(t => Math.Abs(t.Amount.Amount));
                        return new MonthlySummaryDto(
                            Year: g.Key.Year,
                            Month: g.Key.Month,
                            MonthName: new DateTime(g.Key.Year, g.Key.Month, 1).ToString("MMMM yyyy"),
                            TotalIncome: income,
                            TotalExpenses: expenses,
                            NetBalance: income - expenses,
                            TransactionCount: g.Count());
                    })
                    .ToList();

                _logger.LogInformation(
                    "Summary for customer {CustomerId}: {TransactionCount} transactions, income {Income}, expenses {Expenses}",
                    request.CustomerId, transactions.Count, totalIncome, totalExpenses);

                return Result.Success(new TransactionSummaryDto(
                    TotalIncome: totalIncome,
                    TotalExpenses: totalExpenses,
                    NetBalance: totalIncome - totalExpenses,
                    SpendingByCategory: spendingByCategory,
                    TotalTransactions: transactions.Count,
                    MonthlySummaries: monthlySummaries));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get transaction summary for customer {CustomerId}", request.CustomerId);
                return Result.Failure<TransactionSummaryDto>(
                    Error.Failure("SummaryFailed", $"Failed to retrieve summary: {ex.Message}"));
            }
        }
    }
}
