using FluentValidation;
using Modules.WebhookSources.Domain;

namespace Modules.WebhookSources.Application.Common
{
    internal static class InstitutionRules
    {
        /// <summary>Mirrors the domain's invariant so a bad list is a field-level 400, not a 500.</summary>
        public static IRuleBuilderOptions<T, IReadOnlyList<string>> AuthorizedInstitutions<T>(
            this IRuleBuilder<T, IReadOnlyList<string>> rule) =>
            rule.NotEmpty()
                .WithMessage("At least one authorized institution is required.")
                .Must(list => list.All(i => !string.IsNullOrWhiteSpace(i)
                                            && i.Trim().Length <= WebhookSource.MaximumInstitutionNameLength))
                .WithMessage($"Each institution name must be 1-{WebhookSource.MaximumInstitutionNameLength} characters.");
    }
}