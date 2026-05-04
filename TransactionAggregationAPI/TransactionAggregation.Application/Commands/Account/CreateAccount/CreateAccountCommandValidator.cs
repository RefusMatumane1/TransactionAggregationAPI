using FluentValidation;

namespace TransactionAggregation.Application.Commands.Account.CreateAccount
{
    public sealed class CreateAccountCommandValidator : AbstractValidator<CreateAccountCommand>
    {
        public CreateAccountCommandValidator()
        {
            RuleFor(x => x.CustomerId)
                .NotEmpty().WithMessage("Customer ID is required");

            RuleFor(x => x.AccountNumber)
                .NotEmpty().WithMessage("Account number is required")
                .MaximumLength(50).WithMessage("Account number must not exceed 50 characters");

            RuleFor(x => x.AccountName)
                .NotEmpty().WithMessage("Account name is required")
                .MaximumLength(200).WithMessage("Account name must not exceed 200 characters");

            RuleFor(x => x.Currency)
                .NotEmpty().WithMessage("Currency is required")
                .Length(3).WithMessage("Currency must be a 3-character ISO code");
        }
    }
}
