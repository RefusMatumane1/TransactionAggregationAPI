using FluentValidation;
using Modules.Transactions.Application.Common.Aggregation;
using Modules.Transactions.Application.Common.DTOs;

namespace Modules.Transactions.Application.Features.Transactions.Queries.Aggregates
{
    public sealed class GetCashFlowQueryValidator : AbstractValidator<GetCashFlowQuery>
    {
        public const int MaxDailyDays = 366;
        public const int MaxWeeklyDays = 1_830;

        public GetCashFlowQueryValidator()
        {
            RuleFor(x => x.Granularity).IsInEnum();
            RuleFor(x => x.Filter).NotNull().SetValidator(new TransactionFilterValidator());
            RuleFor(x => x.Currency).IsoCurrency();
            this.ValidPeriod(x => x.From, x => x.To);

            RuleFor(x => new ReportingPeriod(x.From, x.To).Days)
                .LessThanOrEqualTo(MaxDailyDays)
                .When(x => x.Granularity == TimeGranularity.Day && x.From <= x.To)
                .OverridePropertyName("granularity")
                .WithMessage($"Daily points are limited to {MaxDailyDays} days; use week or month for longer periods.");

            RuleFor(x => new ReportingPeriod(x.From, x.To).Days)
                .LessThanOrEqualTo(MaxWeeklyDays)
                .When(x => x.Granularity == TimeGranularity.Week && x.From <= x.To)
                .OverridePropertyName("granularity")
                .WithMessage($"Weekly points are limited to {MaxWeeklyDays} days; use month for longer periods.");
        }
    }
}