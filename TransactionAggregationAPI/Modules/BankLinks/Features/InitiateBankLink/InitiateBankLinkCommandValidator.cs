using FluentValidation;

namespace Modules.BankLinks.Features.InitiateBankLink
{
    public sealed class InitiateBankLinkCommandValidator : AbstractValidator<InitiateBankLinkCommand>
    {
        public InitiateBankLinkCommandValidator()
        {
            RuleFor(x => x.CustomerId).NotEmpty();
            RuleFor(x => x.Institution).IsInEnum();
        }
    }
}
