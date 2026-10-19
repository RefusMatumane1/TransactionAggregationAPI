using BuildingBlocks.Application.Querying;
using BuildingBlocks.Application.Validation;
using FluentValidation;
using Modules.Reporting.Application.Aggregation;
using Modules.Reporting.Application.DTOs;

namespace Modules.Reporting.Application.Features.Aggregates
{
    public sealed class GetCashFlowQueryValidator : AbstractValidator<GetCashFlowQuery>
    {
        public const int MaxDailyDays = 366;
        public const int MaxWeeklyDays = 1_830;

        public GetCashFlowQueryValidator()
        {
            RuleFor(x => x.Granularity).IsInEnum();
            RuleFor(x => x.Filter).NotNull().SetValidator(new AccountFilterValidator());
            RuleFor(x => x.Currency!).CurrencyCodeFormat().When(x => x.Currency is not null);
            RuleFor(x => x.ConvertTo!).CurrencyCodeFormat().WithMessage("convertTo must be a 3-letter upper-case currency code, e.g. ZAR.").When(x => x.ConvertTo is not null).OverridePropertyName("convertTo");
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