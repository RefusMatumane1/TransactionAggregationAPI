using FluentValidation;
using Modules.WebhookSources.Domain;

namespace Modules.WebhookSources.Application.Common
{
    internal static class BankRules
    {
        public static IRuleBuilderOptions<T, string> BankCode<T>(this IRuleBuilder<T, string> rule) =>
            rule.Must(WebhookSource.IsValidCode)
                .WithMessage($"Code must be 1-{WebhookSource.MaximumCodeLength} letters, digits, '-' or '_' (e.g. FNB, StandardBank).");

        public static IRuleBuilderOptions<T, string> BankDisplayName<T>(this IRuleBuilder<T, string> rule) =>
            rule.NotEmpty().MaximumLength(WebhookSource.MaximumDisplayNameLength);

        public static IRuleBuilderOptions<T, string> BankColor<T>(this IRuleBuilder<T, string> rule) =>
            rule.Must(WebhookSource.IsValidColor).WithMessage("Colour must be a hex value like #0033A1.");
    }
}