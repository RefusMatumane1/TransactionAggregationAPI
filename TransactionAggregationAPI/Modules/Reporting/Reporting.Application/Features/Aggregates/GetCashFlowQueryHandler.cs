using BuildingBlocks.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Modules.Currency.Contracts;
using Modules.Reporting.Application.Aggregation;
using Modules.Reporting.Application.DTOs;
using Modules.Reporting.Application.Persistence;
using SharedKernel.Common.Models;

namespace Modules.Reporting.Application.Features.Aggregates
{
    internal sealed class GetCashFlowQueryHandler(IReportingDbContext context, ICurrencyCatalog catalog)
        : IQueryHandler<GetCashFlowQuery, CashFlowSeriesDto>
    {
        public async Task<Result<CashFlowSeriesDto>> Handle(GetCashFlowQuery request, CancellationToken cancellationToken)
        {
            var period = new ReportingPeriod(request.From, request.To);
            var resolved = AmountBasis.Resolve(request.Currency, request.ConvertTo, catalog);
            if (resolved.IsFailure)
                return Result.Failure<CashFlowSeriesDto>(resolved.Error);
            var basis = resolved.Value;

            var days = await context.DailyTotals
                .Within(period, request.Filter, basis)
                .GroupBy(d => d.Day)
                .Select(g => new
                {
                    Day = g.Key,
                    Income = g.Sum(d => d.Income),
                    Expenses = g.Sum(d => d.Expenses),
                    Count = g.Sum(d => d.IncomeCount + d.ExpenseCount)
                })
                .ToListAsync(cancellationToken);
            var asOf = await context.AggregationCheckpoints.AsOfAsync(cancellationToken);

            var byBucket = days
                .GroupBy(d => BucketStart(d.Day, request.Granularity))
                .ToDictionary(
                    g => g.Key,
                    g => (Income: g.Sum(d => d.Income), Expenses: g.Sum(d => d.Expenses), Count: g.Sum(d => d.Count)));

            var points = new List<CashFlowPointDto>();
            for (var start = BucketStart(period.From, request.Granularity); start <= period.To; start = Next(start, request.Granularity))
            {
                var end = Next(start, request.Granularity).AddDays(-1);
                var totals = byBucket.GetValueOrDefault(start);
                points.Add(new CashFlowPointDto(
                    start < period.From ? period.From : start,
                    end > period.To ? period.To : end,
                    totals.Income,
                    totals.Expenses,
                    totals.Income - totals.Expenses,
                    totals.Count));
            }

            var income = points.Sum(p => p.Income);
            var expenses = points.Sum(p => p.Expenses);

            return Result.Success(new CashFlowSeriesDto(
                new PeriodDto(period.From, period.To),
                basis.Currency,
                request.Granularity,
                income,
                expenses,
                income - expenses,
                points,
                asOf,
                await context.DailyTotals.DescribeConversionAsync(period, request.Filter, basis, cancellationToken)));
        }

        private static DateOnly BucketStart(DateOnly date, TimeGranularity granularity) => granularity switch
        {
            TimeGranularity.Day => date,
            TimeGranularity.Week => SouthAfricanCalendar.StartOfWeek(date),
            _ => SouthAfricanCalendar.StartOfMonth(date)
        };

        private static DateOnly Next(DateOnly bucketStart, TimeGranularity granularity) => granularity switch
        {
            TimeGranularity.Day => bucketStart.AddDays(1),
            TimeGranularity.Week => bucketStart.AddDays(7),
            _ => bucketStart.AddMonths(1)
        };
    }
}