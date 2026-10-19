using System.Linq.Expressions;

namespace BuildingBlocks.Application.Querying
{
    public static class AccountFilterQueryExtensions
    {
        /// <summary>
        /// Keeps the rows <paramref name="filter"/> admits: those at a bank the caller may read, then those matching the
        /// requested bank, account or account set. The caller says where a row keeps its bank and account id, so one rule
        /// serves every table keyed by (institution, account) — the ledger and the totals built from it alike.
        /// </summary>
        public static IQueryable<T> InScope<T>(
            this IQueryable<T> rows,
            AccountFilter filter,
            Expression<Func<T, string>> institution,
            Expression<Func<T, string>> externalAccountId)
        {
            if (!filter.Access.AllInstitutions)
                rows = rows.Where(AnyOf<T>(filter.Access.Institutions.ToList(), institution));

            if (filter.Institution is { Length: > 0 } bank)
                rows = rows.Where(EqualTo(institution, bank));
            if (filter.ExternalAccountId is { Length: > 0 } account)
                rows = rows.Where(EqualTo(externalAccountId, account));
            if (filter.Accounts is { } accounts)
                rows = rows.Where(AnyAccount(accounts, institution, externalAccountId));

            return rows;
        }

        // Each predicate reads its value through a member of a constant holder, which is how a captured variable
        // appears in an expression tree: the provider turns it into a query parameter rather than inlining it, so
        // the SQL text stays the same however many different values callers filter by.
        private static Expression<Func<T, bool>> AnyOf<T>(List<string> values, Expression<Func<T, string>> selector)
        {
            var row = Expression.Parameter(typeof(T), "row");
            var column = new Rebind(selector.Parameters[0], row).Visit(selector.Body);
            var list = Expression.Property(Expression.Constant(new Holder<List<string>>(values)), nameof(Holder<List<string>>.Value));

            return Expression.Lambda<Func<T, bool>>(
                Expression.Call(typeof(Enumerable), nameof(Enumerable.Contains), [typeof(string)], list, column), row);
        }

        private static Expression<Func<T, bool>> EqualTo<T>(Expression<Func<T, string>> selector, string value)
        {
            var row = Expression.Parameter(typeof(T), "row");
            var column = new Rebind(selector.Parameters[0], row).Visit(selector.Body);
            var captured = Expression.Property(Expression.Constant(new Holder<string>(value)), nameof(Holder<string>.Value));

            return Expression.Lambda<Func<T, bool>>(Expression.Equal(column, captured), row);
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
                var captured = Expression.Constant(key);
                match = Expression.OrElse(match, Expression.AndAlso(
                    Expression.Equal(bank, Expression.Property(captured, nameof(AccountKey.Institution))),
                    Expression.Equal(accountId, Expression.Property(captured, nameof(AccountKey.ExternalAccountId)))));
            }

            return Expression.Lambda<Func<T, bool>>(match, row);
        }

        private sealed record Holder<TValue>(TValue Value);

        private sealed class Rebind(ParameterExpression from, ParameterExpression to) : ExpressionVisitor
        {
            protected override Expression VisitParameter(ParameterExpression node) => node == from ? to : node;
        }
    }
}
