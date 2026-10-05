using FluentValidation;
using Modules.Transactions.Application.Common.Aggregation;

namespace Modules.Transactions.Application.Features.Transactions.Queries.Aggregates
{
    internal static class AggregateQueryRules
    {
        public static void ValidPeriod<T>(this AbstractValidator<T> validator, Func<T, DateOnly> from, Func<T, DateOnly> to)
        {
            validator.RuleFor(x => from(x))
                .Must((x, start) => start <= to(x))
                .OverridePropertyName("from")
                .WithMessage("from must be on or before to.");

            validator.RuleFor(x => new ReportingPeriod(from(x), to(x)).Days)
                .LessThanOrEqualTo(ReportingPeriod.MaxDays)
                .When(x => from(x) <= to(x))
                .OverridePropertyName("to")
                .WithMessage($"A period may cover at most {ReportingPeriod.MaxDays} days.");
        }
    }
}