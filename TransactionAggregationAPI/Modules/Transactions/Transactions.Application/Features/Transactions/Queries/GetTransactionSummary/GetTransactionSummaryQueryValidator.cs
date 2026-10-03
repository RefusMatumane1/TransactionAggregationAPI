using FluentValidation;
using Modules.Transactions.Application.Common.Aggregation;

namespace Modules.Transactions.Application.Features.Transactions.Queries.GetTransactionSummary
{
    public sealed class GetTransactionSummaryQueryValidator : AbstractValidator<GetTransactionSummaryQuery>
    {
        public static readonly TimeSpan MaxPeriod = TimeSpan.FromDays(366 * 10);

        public GetTransactionSummaryQueryValidator()
        {
            RuleFor(x => x.Filter).NotNull().SetValidator(new TransactionFilterValidator());
            RuleFor(x => x.Currency).IsoCurrency();
            RuleFor(x => x.StartDate)
                .LessThanOrEqualTo(x => x.EndDate)
                .WithMessage("startDate must not be after endDate.");
            RuleFor(x => x)
                .Must(x => x.EndDate - x.StartDate <= MaxPeriod)
                .WithName("endDate")
                .WithMessage("A summary may cover at most 10 years.");
        }
    }
}