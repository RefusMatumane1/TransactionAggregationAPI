using BuildingBlocks.Application.Pagination;
using FluentValidation;
using Modules.Transactions.Application.Common.Aggregation;
using Modules.Transactions.Application.Common.Pagination;

namespace Modules.Transactions.Application.Features.Transactions.Queries.GetTransactions
{
    public sealed class GetTransactionsQueryValidator : AbstractValidator<GetTransactionsQuery>
    {
        public const int MaxPageSize = 100;
        public const int MinSearchLength = 3;
        public const int MaxSearchLength = 100;

        public GetTransactionsQueryValidator()
        {
            RuleFor(x => x.Filter).NotNull().SetValidator(new TransactionFilterValidator());

            RuleFor(x => x.PageSize)
                .InclusiveBetween(1, MaxPageSize).WithMessage($"Page size must be between 1 and {MaxPageSize}");

            RuleFor(x => x.Category).IsInEnum().When(x => x.Category.HasValue);
            RuleFor(x => x.Currency!).IsoCurrency().When(x => x.Currency is not null);

            RuleFor(x => x.SearchTerm!.Trim())
                .Length(MinSearchLength, MaxSearchLength)
                .When(x => !string.IsNullOrWhiteSpace(x.SearchTerm))
                .OverridePropertyName("searchTerm")
                .WithMessage($"searchTerm must be {MinSearchLength}-{MaxSearchLength} characters.");

            RuleFor(x => x.SortBy)
                .Must(TransactionSorts.IsKnown)
                .WithMessage($"sortBy must be one of: {string.Join(", ", TransactionSorts.Names)}.");

            RuleFor(x => x.Cursor)
                .Must((query, cursor) => PageCursor.TryDecode(cursor, out var decoded)
                    && TransactionSorts.Resolve(query.SortBy).IsValid(decoded!, query.SortDescending))
                .When(x => x.Cursor is not null && TransactionSorts.IsKnown(x.SortBy))
                .WithMessage("cursor is invalid, or was issued for a different sort order.");

            RuleFor(x => x.MinAmount).GreaterThanOrEqualTo(0).When(x => x.MinAmount.HasValue);
            RuleFor(x => x.MaxAmount).GreaterThanOrEqualTo(0).When(x => x.MaxAmount.HasValue);
            RuleFor(x => x.MinAmount)
                .LessThanOrEqualTo(x => x.MaxAmount)
                .When(x => x.MinAmount.HasValue && x.MaxAmount.HasValue)
                .WithMessage("Minimum amount cannot be greater than maximum amount");

            RuleFor(x => x.FromDate)
                .LessThanOrEqualTo(x => x.ToDate)
                .When(x => x.FromDate.HasValue && x.ToDate.HasValue)
                .WithMessage("From date cannot be after to date");
        }
    }
}