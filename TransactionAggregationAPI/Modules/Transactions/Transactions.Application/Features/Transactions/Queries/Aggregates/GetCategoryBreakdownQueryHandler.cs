using BuildingBlocks.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Modules.Transactions.Application.Common.Aggregation;
using Modules.Transactions.Application.Common.DTOs;
using Modules.Transactions.Application.Common.Interfaces;
using SharedKernel.Common.Models;

namespace Modules.Transactions.Application.Features.Transactions.Queries.Aggregates
{
    internal sealed class GetCategoryBreakdownQueryHandler(ITransactionsDbContext context)
        : IQueryHandler<GetCategoryBreakdownQuery, CategoryBreakdownDto>
    {
        public async Task<Result<CategoryBreakdownDto>> Handle(GetCategoryBreakdownQuery request, CancellationToken cancellationToken)
        {
            var period = new ReportingPeriod(request.From, request.To);
            var totals = context.DailyTotals.Within(period, request.Filter, request.Currency);
            var grouped = request.Direction == CashFlowDirection.Expense
                ? totals.Where(d => d.ExpenseCount > 0)
                    .GroupBy(d => d.Category)
                    .Select(g => new { Category = g.Key, Total = g.Sum(d => d.Expenses), Count = g.Sum(d => d.ExpenseCount) })
                : totals.Where(d => d.IncomeCount > 0)
                    .GroupBy(d => d.Category)
                    .Select(g => new { Category = g.Key, Total = g.Sum(d => d.Income), Count = g.Sum(d => d.IncomeCount) });

            var rows = await grouped.ToListAsync(cancellationToken);
            var asOf = await context.AggregationCheckpoints.AsOfAsync(cancellationToken);

            var total = rows.Sum(r => r.Total);
            var categories = rows
                .Select(r => new CategoryTotalDto(
                    r.Category,
                    r.Total,
                    r.Count,
                    total == 0 ? 0 : Math.Round(r.Total / total, 4)))
                .OrderByDescending(c => c.Amount)
                .ThenBy(c => c.Category)
                .ToList();

            return Result.Success(new CategoryBreakdownDto(
                new PeriodDto(period.From, period.To),
                request.Currency,
                request.Direction,
                total,
                rows.Sum(r => r.Count),
                categories,
                asOf));
        }
    }
}