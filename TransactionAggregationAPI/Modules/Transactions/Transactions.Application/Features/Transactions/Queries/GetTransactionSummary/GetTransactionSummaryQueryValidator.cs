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
            RuleFor(x => x.Currency!).CurrencyCodeFormat().When(x => x.Currency is not null);
            RuleFor(x => x.ConvertTo!).CurrencyCodeFormat().WithMessage("convertTo must be a 3-letter upper-case currency code, e.g. ZAR.").When(x => x.ConvertTo is not null).OverridePropertyName("convertTo");
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