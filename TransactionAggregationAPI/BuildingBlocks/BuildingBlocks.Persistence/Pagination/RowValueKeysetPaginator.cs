using BuildingBlocks.Application.Pagination;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace BuildingBlocks.Persistence.Pagination
{
    public sealed class RowValueKeysetPaginator : IKeysetPaginator
    {
        private static readonly MethodInfo LessThan = RowComparison(nameof(NpgsqlDbFunctionsExtensions.LessThan));
        private static readonly MethodInfo GreaterThan = RowComparison(nameof(NpgsqlDbFunctionsExtensions.GreaterThan));
        private static readonly Expression Functions = Expression.Property(null, typeof(EF), nameof(EF.Functions));

        public IQueryable<T> After<T, TKey, TTieBreaker>(
            IQueryable<T> source,
            Expression<Func<T, TKey>> key,
            Expression<Func<T, TTieBreaker>> tieBreaker,
            TKey lastKey,
            TTieBreaker lastTieBreaker,
            bool descending)
        {
            var row = key.Parameters[0];
            var tieBreakerBody = new ParameterRebinder(tieBreaker.Parameters[0], row).Visit(tieBreaker.Body);
            var tuple = typeof(ValueTuple<TKey, TTieBreaker>).GetConstructor([typeof(TKey), typeof(TTieBreaker)])!;

            var boundary = Expression.Constant(new Boundary<TKey, TTieBreaker>(lastKey, lastTieBreaker));
            var current = Expression.Convert(Expression.New(tuple, key.Body, tieBreakerBody), typeof(ITuple));
            var last = Expression.Convert(
                Expression.New(
                    tuple,
                    Expression.Property(boundary, nameof(Boundary<TKey, TTieBreaker>.Key)),
                    Expression.Property(boundary, nameof(Boundary<TKey, TTieBreaker>.TieBreaker))),
                typeof(ITuple));

            var comparison = Expression.Call(descending ? LessThan : GreaterThan, Functions, current, last);
            return source.Where(Expression.Lambda<Func<T, bool>>(comparison, row));
        }

        private static MethodInfo RowComparison(string name) =>
            typeof(NpgsqlDbFunctionsExtensions).GetMethod(name, [typeof(DbFunctions), typeof(ITuple), typeof(ITuple)])
            ?? throw new InvalidOperationException($"Npgsql row value comparison '{name}' is not available.");

        private sealed class Boundary<TKey, TTieBreaker>(TKey key, TTieBreaker tieBreaker)
        {
            public TKey Key { get; } = key;
            public TTieBreaker TieBreaker { get; } = tieBreaker;
        }

        private sealed class ParameterRebinder(ParameterExpression from, ParameterExpression to) : ExpressionVisitor
        {
            protected override Expression VisitParameter(ParameterExpression node) => node == from ? to : node;
        }
    }
}