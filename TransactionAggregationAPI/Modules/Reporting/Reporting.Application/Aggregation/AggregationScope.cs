using BuildingBlocks.Application.Querying;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Modules.Currency.Contracts;
using Modules.Reporting.Application.DTOs;
using SharedKernel.Common.ValueObjects;
using System.Linq.Expressions;

namespace Modules.Reporting.Application.Aggregation
{
    internal static class AggregationScope
    {
        public static IQueryable<DailyTotal> InScope(
            this IQueryable<DailyTotal> totals, DateOnly from, DateOnly to, AccountFilter filter) =>
            totals
                .AsNoTracking()
                .Where(d => d.Day >= from && d.Day <= to)
                .InScope(filter, d => d.SourceName, d => d.ExternalAccountId);

        public static IQueryable<TotalRow> Within(
            this IQueryable<DailyTotal> totals, DateOnly from, DateOnly to, AccountFilter filter, AmountBasis basis)
        {
            var scoped = totals.InScope(from, to, filter);

            if (!basis.IsConverting)
            {
                var currency = basis.Currency;
                return scoped.Where(d => d.Currency == currency).Select(Identity);
            }

            var target = basis.ConvertTo!;
            var convertible = basis.Catalog.Supported.ToList();
            return scoped.Where(d => convertible.Contains(d.Currency)).Select(Converting(basis.Catalog, target));
        }

        public static IQueryable<TotalRow> Within(
            this IQueryable<DailyTotal> totals, ReportingPeriod period, AccountFilter filter, AmountBasis basis) =>
            totals.Within(period.From, period.To, filter, basis);

        public static async Task<ConversionDto?> DescribeConversionAsync(
            this IQueryable<DailyTotal> totals, DateOnly from, DateOnly to, AccountFilter filter, AmountBasis basis,
            CancellationToken cancellationToken)
        {
            if (!basis.IsConverting)
                return null;

            var catalog = basis.Catalog;
            var target = basis.ConvertTo!;
            var present = await totals.InScope(from, to, filter).Select(d => d.Currency).Distinct().ToListAsync(cancellationToken);

            return new ConversionDto(
                target,
                catalog.Version,
                [.. present.Where(catalog.IsSupported).Order(StringComparer.Ordinal)
                    .Select(c => new ConversionRateDto(c, catalog.Rate(c, target)!.Value))],
                [.. present.Where(c => !catalog.IsSupported(c)).Order(StringComparer.Ordinal)]);
        }

        public static Task<ConversionDto?> DescribeConversionAsync(
            this IQueryable<DailyTotal> totals, ReportingPeriod period, AccountFilter filter, AmountBasis basis,
            CancellationToken cancellationToken) =>
            totals.DescribeConversionAsync(period.From, period.To, filter, basis, cancellationToken);

        private static readonly Expression<Func<DailyTotal, TotalRow>> Identity = d => new TotalRow
        {
            Day = d.Day,
            SourceName = d.SourceName,
            ExternalAccountId = d.ExternalAccountId,
            Category = d.Category,
            Currency = d.Currency,
            Income = d.Income,
            Expenses = d.Expenses,
            IncomeCount = d.IncomeCount,
            ExpenseCount = d.ExpenseCount
        };

        private static decimal RateFor(DailyTotal row) => throw new InvalidOperationException("Replaced before translation.");

        private static readonly Expression<Func<DailyTotal, TotalRow>> ConvertingTemplate = d => new TotalRow
        {
            Day = d.Day,
            SourceName = d.SourceName,
            ExternalAccountId = d.ExternalAccountId,
            Category = d.Category,
            Currency = d.Currency,
            Income = Math.Round(d.Income * RateFor(d), MoneyScale.Max),
            Expenses = Math.Round(d.Expenses * RateFor(d), MoneyScale.Max),
            IncomeCount = d.IncomeCount,
            ExpenseCount = d.ExpenseCount
        };

        private static Expression<Func<DailyTotal, TotalRow>> Converting(ICurrencyCatalog catalog, string target)
        {
            var row = Expression.Parameter(typeof(DailyTotal), "row");
            Expression rate = Expression.Constant(0m);
            foreach (var code in catalog.Supported)
            {
                rate = Expression.Condition(
                    Expression.Equal(Expression.Property(row, nameof(DailyTotal.Currency)), Expression.Constant(code)),
                    Expression.Constant(catalog.Rate(code, target)!.Value),
                    rate);
            }

            return (Expression<Func<DailyTotal, TotalRow>>)new RateSubstitution(row, rate).Visit(ConvertingTemplate);
        }

        private sealed class RateSubstitution(ParameterExpression row, Expression rate) : ExpressionVisitor
        {
            protected override Expression VisitMethodCall(MethodCallExpression node)
            {
                if (node.Method.Name != nameof(RateFor))
                    return base.VisitMethodCall(node);

                return new Rebind(row, (ParameterExpression)node.Arguments[0]).Visit(rate);
            }
        }

        public static Task<DateTime?> AsOfAsync(this IQueryable<AggregationCheckpoint> checkpoints, CancellationToken cancellationToken) =>
            checkpoints
                .AsNoTracking()
                .Where(c => c.Id == AggregationCheckpoint.SingletonId)
                .Select(c => (DateTime?)c.AsOf)
                .FirstOrDefaultAsync(cancellationToken);

        private sealed class Rebind(ParameterExpression from, ParameterExpression to) : ExpressionVisitor
        {
            protected override Expression VisitParameter(ParameterExpression node) => node == from ? to : node;
        }
    }
}