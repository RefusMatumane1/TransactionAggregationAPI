using BuildingBlocks.Application.Abstractions.Authentication;
using Microsoft.EntityFrameworkCore;
using Modules.Transactions.Domain.Entities;
using Modules.Transactions.Domain.Services;

namespace Modules.Transactions.Application.Common.Aggregation
{
    // The single specification every read applies: ledger entries (or their daily totals) only,
    // restricted to the institutions the caller may read, then narrowed by the request's own filter.
    internal static class AggregationScope
    {
        public static IQueryable<Transaction> Ledger(this IQueryable<Transaction> transactions, TransactionFilter filter) =>
            transactions
                .AsNoTracking()
                .Where(LedgerEntries.IsEntry)
                .VisibleTo(filter.Access)
                .Matching(filter);

        // The same specification over the daily read model the aggregate queries use.
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
            return totals;
        }

        public static IQueryable<DailyTotal> Within(
            this IQueryable<DailyTotal> totals, ReportingPeriod period, TransactionFilter filter, string currency) =>
            totals.Within(period.From, period.To, filter, currency);

        // When the totals were last rebuilt; null until the first refresh has run.
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
            return transactions;
        }
    }
}