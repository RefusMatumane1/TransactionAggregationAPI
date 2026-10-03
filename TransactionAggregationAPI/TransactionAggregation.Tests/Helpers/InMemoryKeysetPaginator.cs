using BuildingBlocks.Application.Pagination;
using System.Linq.Expressions;

namespace TransactionAggregation.Tests.Helpers
{
    public sealed class InMemoryKeysetPaginator : IKeysetPaginator
    {
        public IQueryable<T> After<T, TKey, TTieBreaker>(
            IQueryable<T> source,
            Expression<Func<T, TKey>> key,
            Expression<Func<T, TTieBreaker>> tieBreaker,
            TKey lastKey,
            TTieBreaker lastTieBreaker,
            bool descending)
        {
            var row = key.Parameters[0];
            var tieBreakerBody = new Rebinder(tieBreaker.Parameters[0], row).Visit(tieBreaker.Body);

            var byKey = Compare(key.Body, lastKey);
            var byTieBreaker = Compare(tieBreakerBody, lastTieBreaker);
            var zero = Expression.Constant(0);

            Expression Beyond(Expression comparison) =>
                descending ? Expression.LessThan(comparison, zero) : Expression.GreaterThan(comparison, zero);

            var predicate = Expression.OrElse(
                Beyond(byKey),
                Expression.AndAlso(Expression.Equal(byKey, zero), Beyond(byTieBreaker)));

            return source.Where(Expression.Lambda<Func<T, bool>>(predicate, row));
        }

        private static MethodCallExpression Compare<TValue>(Expression value, TValue boundary) =>
            Expression.Call(
                Expression.Constant(Comparer<TValue>.Default),
                typeof(IComparer<TValue>).GetMethod(nameof(IComparer<TValue>.Compare))!,
                value,
                Expression.Constant(boundary, typeof(TValue)));

        private sealed class Rebinder(ParameterExpression from, ParameterExpression to) : ExpressionVisitor
        {
            protected override Expression VisitParameter(ParameterExpression node) => node == from ? to : node;
        }
    }
}