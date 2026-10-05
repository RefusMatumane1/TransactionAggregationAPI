using BuildingBlocks.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Modules.Transactions.Application.Common.Aggregation;
using Modules.Transactions.Application.Common.DTOs;
using Modules.Transactions.Application.Common.Interfaces;
using SharedKernel.Common.Models;
using System.Globalization;

namespace Modules.Transactions.Application.Features.Transactions.Queries.GetTransactionSummary
{
    internal sealed class GetTransactionSummaryQueryHandler(ITransactionsDbContext context)
        : IQueryHandler<GetTransactionSummaryQuery, TransactionSummaryDto>
    {
        public async Task<Result<TransactionSummaryDto>> Handle(
            GetTransactionSummaryQuery request,
            CancellationToken cancellationToken)
        {
            // The read model is daily, so the bounds are the South African days they fall on (both inclusive).
            var from = SouthAfricanCalendar.DayOf(DateTime.SpecifyKind(request.StartDate, DateTimeKind.Utc));
            var to = SouthAfricanCalendar.DayOf(DateTime.SpecifyKind(request.EndDate, DateTimeKind.Utc));

            var buckets = await context.DailyTotals
                .Within(from, to, request.Filter, request.Currency)
                .GroupBy(d => new { d.Day.Year, d.Day.Month, d.Category })
                .Select(g => new
                {
                    g.Key.Year,
                    g.Key.Month,
                    g.Key.Category,
                    Income = g.Sum(d => d.Income),
                    Expenses = g.Sum(d => d.Expenses),
                    ExpenseCount = g.Sum(d => d.ExpenseCount),
                    Count = g.Sum(d => d.IncomeCount + d.ExpenseCount)
                })
                .ToListAsync(cancellationToken);
            var asOf = await context.AggregationCheckpoints.AsOfAsync(cancellationToken);

            var monthlySummaries = buckets
                .GroupBy(b => (b.Year, b.Month))
                .OrderBy(g => g.Key.Year)
                .ThenBy(g => g.Key.Month)
                .Select(g =>
                {
                    var income = g.Sum(b => b.Income);
                    var expenses = g.Sum(b => b.Expenses);
                    return new MonthlySummaryDto(
                        Year: g.Key.Year,
                        Month: g.Key.Month,
                        MonthName: new DateTime(g.Key.Year, g.Key.Month, 1).ToString("MMMM yyyy", CultureInfo.InvariantCulture),
                        TotalIncome: income,
                        TotalExpenses: expenses,
                        NetBalance: income - expenses,
                        TransactionCount: g.Sum(b => b.Count));
                })
                .ToList();

            var spendingByCategory = buckets
                .Where(b => b.ExpenseCount > 0)
                .GroupBy(b => b.Category)
                .ToDictionary(g => g.Key, g => g.Sum(b => b.Expenses));

            var totalIncome = monthlySummaries.Sum(m => m.TotalIncome);
            var totalExpenses = monthlySummaries.Sum(m => m.TotalExpenses);

            return Result.Success(new TransactionSummaryDto(
                TotalIncome: totalIncome,
                TotalExpenses: totalExpenses,
                NetBalance: totalIncome - totalExpenses,
                SpendingByCategory: spendingByCategory,
                TotalTransactions: monthlySummaries.Sum(m => m.TransactionCount),
                MonthlySummaries: monthlySummaries,
                Currency: request.Currency,
                AsOf: asOf));
        }
    }
}