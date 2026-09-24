using FluentValidation;

namespace Modules.Transactions.Application.Features.Transactions.Queries.ExportTransactions
{
    public sealed class ExportTransactionsQueryValidator : AbstractValidator<ExportTransactionsQuery>
    {
        public ExportTransactionsQueryValidator()
        {
            RuleFor(x => x.CustomerId).NotEmpty();
            RuleFor(x => x.FromDate)
                .LessThanOrEqualTo(x => x.ToDate)
                .When(x => x.FromDate.HasValue && x.ToDate.HasValue)
                .WithMessage("fromDate must not be after toDate.");
            RuleFor(x => x.Format)
                .Must(f => f is null || f.Equals("csv", StringComparison.OrdinalIgnoreCase))
                .WithMessage("Only the csv format is supported.");
        }
    }
}