using FluentValidation;
using Modules.Transactions.Domain.Common.ValueObjects;
using Modules.Transactions.Domain.Entities;
using SharedKernel.Common.ValueObjects;

namespace Modules.Transactions.Application.Common.Aggregation
{
    public sealed class TransactionFilterValidator : AbstractValidator<TransactionFilter>
    {
        public TransactionFilterValidator()
        {
            RuleFor(x => x.Access).NotNull();
            RuleFor(x => x.Institution).MaximumLength(TransactionSource.MaxNameLength).OverridePropertyName("institution");
            RuleFor(x => x.ExternalAccountId).MaximumLength(Transaction.MaxExternalAccountIdLength).OverridePropertyName("externalAccountId");
            RuleFor(x => x.Institution)
                .NotEmpty()
                .When(x => !string.IsNullOrEmpty(x.ExternalAccountId))
                .OverridePropertyName("institution")
                .WithMessage("institution is required with externalAccountId: an account id is only unique within its bank.");
        }
    }

    public static class CurrencyRules
    {
        public static IRuleBuilderOptions<T, string> IsoCurrency<T>(this IRuleBuilder<T, string> rule) =>
            rule.Must(c => SupportedCurrency.IsSupported(c) && c == SupportedCurrency.Normalize(c))
                .WithMessage("currency must be an upper-case ISO 4217 code, e.g. ZAR.");
    }
}