using BuildingBlocks.Application.Abstractions.Authentication;
using Microsoft.EntityFrameworkCore;
using Modules.Transactions.Application.Common.Currency;
using Modules.Transactions.Application.Common.DTOs;
using Modules.Transactions.Domain.Common.ValueObjects;
using Modules.Transactions.Domain.Entities;
using Modules.Transactions.Domain.Services;
using System.Linq.Expressions;

namespace Modules.Transactions.Application.Common.Aggregation
{
    internal static class AggregationScope
    {
        public static IQueryable<Transaction> Ledger(this IQueryable<Transaction> transactions, TransactionFilter filter) =>
            transactions
                .AsNoTracking()
                .Where(LedgerEntries.IsEntry)
                .VisibleTo(filter.Access)
                .Matching(filter);

        public static IQueryable<DailyTotal> InScope(
            this IQueryable<DailyTotal> totals, DateOnly from, DateOnly to, TransactionFilter filter)
        {
            totals = totals.AsNoTracking().Where(d => d.Day >= from && d.Day <= to);

            if (!filter.Access.AllInstitutions)
            {
                var institutions = filter.Access.Institutions.ToList();
                totals = totals.Where(d => institutions.Contains(d.SourceName));
            }

            if (filter.Institution is { Length: > 0 } institution)
                totals = totals.Where(d => d.SourceName == institution);
            if (filter.ExternalAccountId is { Length: > 0 } account)
                totals = totals.Where(d => d.ExternalAccountId == account);
            if (filter.Accounts is { } accounts)
                totals = totals.Where(AnyAccount<DailyTotal>(accounts, d => d.SourceName, d => d.ExternalAccountId));
            return totals;
        }

        public static IQueryable<TotalRow> Within(
            this IQueryable<DailyTotal> totals, DateOnly from, DateOnly to, TransactionFilter filter, AmountBasis basis)
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
            this IQueryable<DailyTotal> totals, ReportingPeriod period, TransactionFilter filter, AmountBasis basis) =>
            totals.Within(period.From, period.To, filter, basis);

        public static async Task<ConversionDto?> DescribeConversionAsync(
            this IQueryable<DailyTotal> totals, DateOnly from, DateOnly to, TransactionFilter filter, AmountBasis basis,
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
            this IQueryable<DailyTotal> totals, ReportingPeriod period, TransactionFilter filter, AmountBasis basis,
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
            Income = Math.Round(d.Income * RateFor(d), Money.MaxScale),
            Expenses = Math.Round(d.Expenses * RateFor(d), Money.MaxScale),
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

        private static IQueryable<Transaction> VisibleTo(this IQueryable<Transaction> transactions, InstitutionAccess access)
        {
            if (access.AllInstitutions)
                return transactions;

            var institutions = access.Institutions.ToList();
            return transactions.Where(t => institutions.Contains(t.Source.Name));
        }

        private static IQueryable<Transaction> Matching(this IQueryable<Transaction> transactions, TransactionFilter filter)
        {
            if (filter.Institution is { Length: > 0 } institution)
                transactions = transactions.Where(t => t.Source.Name == institution);
            if (filter.ExternalAccountId is { Length: > 0 } account)
                transactions = transactions.Where(t => t.ExternalAccountId == account);
            if (filter.Accounts is { } accounts)
                transactions = transactions.Where(AnyAccount<Transaction>(accounts, t => t.Source.Name, t => t.ExternalAccountId));
            return transactions;
        }

        private static Expression<Func<T, bool>> AnyAccount<T>(
            IReadOnlyList<AccountKey> accounts,
            Expression<Func<T, string>> institution,
            Expression<Func<T, string>> externalAccountId)
        {
            var row = Expression.Parameter(typeof(T), "row");
            var bank = new Rebind(institution.Parameters[0], row).Visit(institution.Body);
            var accountId = new Rebind(externalAccountId.Parameters[0], row).Visit(externalAccountId.Body);

            Expression match = Expression.Constant(false);
            foreach (var key in accounts)
            {
                var captured = Expression.Constant(new Captured(key.Institution, key.ExternalAccountId));
                match = Expression.OrElse(match, Expression.AndAlso(
                    Expression.Equal(bank, Expression.Property(captured, nameof(Captured.Institution))),
                    Expression.Equal(accountId, Expression.Property(captured, nameof(Captured.ExternalAccountId)))));
            }

            return Expression.Lambda<Func<T, bool>>(match, row);
        }

        private sealed record Captured(string Institution, string ExternalAccountId);

        private sealed class Rebind(ParameterExpression from, ParameterExpression to) : ExpressionVisitor
        {
            protected override Expression VisitParameter(ParameterExpression node) => node == from ? to : node;
        }
    }
}