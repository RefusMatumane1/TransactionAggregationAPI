using BuildingBlocks.Application.Abstractions.Authentication;
using Microsoft.EntityFrameworkCore;
using Modules.Transactions.Domain.Entities;
using Modules.Transactions.Domain.Services;
using System.Linq.Expressions;

namespace Modules.Transactions.Application.Common.Aggregation
{
    // The one specification every read applies: ledger entries only, restricted to the caller's institutions,
    // then narrowed by the filter.
    internal static class AggregationScope
    {
        public static IQueryable<Transaction> Ledger(this IQueryable<Transaction> transactions, TransactionFilter filter) =>
            transactions
                .AsNoTracking()
                .Where(LedgerEntries.IsEntry)
                .VisibleTo(filter.Access)
                .Matching(filter);

        public static IQueryable<DailyTotal> Within(
            this IQueryable<DailyTotal> totals, DateOnly from, DateOnly to, TransactionFilter filter, string currency)
        {
            totals = totals.AsNoTracking().Where(d => d.Currency == currency && d.Day >= from && d.Day <= to);

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

        public static IQueryable<DailyTotal> Within(
            this IQueryable<DailyTotal> totals, ReportingPeriod period, TransactionFilter filter, string currency) =>
            totals.Within(period.From, period.To, filter, currency);

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

        // (bank = b1 AND account = a1) OR ...: equality seeks on the (bank, account, ...) indexes; values stay
        // SQL parameters, so the plan is shared across customers.
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