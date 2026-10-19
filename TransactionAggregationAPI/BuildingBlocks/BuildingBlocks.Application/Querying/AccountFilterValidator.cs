using FluentValidation;

namespace BuildingBlocks.Application.Querying
{
    public sealed class AccountFilterValidator : AbstractValidator<AccountFilter>
    {
        public AccountFilterValidator()
        {
            RuleFor(x => x.Access).NotNull();
            RuleFor(x => x.Institution).MaximumLength(AccountFilter.MaxInstitutionLength).OverridePropertyName("institution");
            RuleFor(x => x.ExternalAccountId).MaximumLength(AccountFilter.MaxExternalAccountIdLength).OverridePropertyName("externalAccountId");
            RuleFor(x => x.Institution)
                .NotEmpty()
                .When(x => !string.IsNullOrEmpty(x.ExternalAccountId))
                .OverridePropertyName("institution")
                .WithMessage("institution is required with externalAccountId: an account id is only unique within its bank.");
            RuleFor(x => x.Accounts!.Count)
                .LessThanOrEqualTo(AccountFilter.MaxAccounts)
                .When(x => x.Accounts is not null)
                .OverridePropertyName("accounts");
        }
    }
}
