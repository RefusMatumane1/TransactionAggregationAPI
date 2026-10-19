using FluentValidation;
using SharedKernel.Common.ValueObjects;

namespace BuildingBlocks.Application.Validation
{
    public static class CurrencyRules
    {
        public static IRuleBuilderOptions<T, string> CurrencyCodeFormat<T>(this IRuleBuilder<T, string> rule) =>
            rule.Must(CurrencyCode.IsCanonical)
                .WithMessage("currency must be a 3-letter upper-case currency code, e.g. ZAR.");
    }
}
