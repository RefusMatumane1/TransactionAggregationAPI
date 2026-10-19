using BuildingBlocks.Application.Abstractions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Modules.Currency.Contracts;
using Modules.Reporting.Application.Aggregation;
using Modules.Reporting.Application.DTOs;
using Modules.Reporting.Application.Persistence;
using SharedKernel.Common.Models;
using System.Globalization;

namespace Modules.Reporting.Application.Features.GetTransactionSummary
{
    internal sealed class GetTransactionSummaryQueryHandler(IReportingDbContext context, ICurrencyCatalog catalog)
        : IQueryHandler<GetTransactionSummaryQuery, TransactionSummaryDto>
    {
        public async Task<Result<TransactionSummaryDto>> Handle(
            GetTransactionSummaryQuery request,
            CancellationToken cancellationToken)
        {
            var from = SouthAfricanCalendar.DayOf(DateTime.SpecifyKind(request.StartDate, DateTimeKind.Utc));
            var to = SouthAfricanCalendar.DayOf(DateTime.SpecifyKind(request.EndDate, DateTimeKind.Utc));
            var resolved = AmountBasis.Resolve(request.Currency, request.ConvertTo, catalog);
            if (resolved.IsFailure)
                return Result.Failure<TransactionSummaryDto>(resolved.Error);
            var basis = resolved.Value;

            var buckets = await context.DailyTotals
                .Within(from, to, request.Filter, basis)
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
                Currency: basis.Currency,
                AsOf: asOf,
                Conversion: await context.DailyTotals.DescribeConversionAsync(from, to, request.Filter, basis, cancellationToken)));
        }
    }
}