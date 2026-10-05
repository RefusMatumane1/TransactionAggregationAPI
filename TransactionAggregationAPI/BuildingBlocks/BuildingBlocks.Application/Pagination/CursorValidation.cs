using FluentValidation;

namespace BuildingBlocks.Application.Pagination
{
    public static class CursorValidation
    {
        public static IRuleBuilderOptions<T, string?> MustBeACursorFor<T, TItem>(
            this IRuleBuilder<T, string?> rule, KeysetSort<TItem> sort, bool descending) =>
            rule.Must(cursor => cursor is null || (PageCursor.TryDecode(cursor, out var decoded) && sort.IsValid(decoded!, descending)))
                .WithMessage("cursor is invalid.");
    }
}