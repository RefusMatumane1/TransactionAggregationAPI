using FluentValidation;

namespace TransactionAggregation.Application.Commands.CreateTransaction
{
    public sealed class CreateTransactionCommandValidator : AbstractValidator<CreateTransactionCommand>
    {
        public CreateTransactionCommandValidator()
        {
            RuleFor(x => x.CustomerId)
                .NotEmpty().WithMessage("Customer ID is required");

            RuleFor(x => x.Amount)
                .NotEqual(0).WithMessage("Transaction amount cannot be zero");

            RuleFor(x => x.Currency)
                .NotEmpty().WithMessage("Currency is required")
                .Length(3).WithMessage("Currency must be a 3-character ISO code");

            RuleFor(x => x.TransactionDate)
                .NotEmpty().WithMessage("Transaction date is required")
                .LessThanOrEqualTo(DateTime.UtcNow.AddDays(1))
                .WithMessage("Transaction date cannot be in the future");

            RuleFor(x => x.Description)
                .NotEmpty().WithMessage("Description is required")
                .MaximumLength(500).WithMessage("Description must not exceed 500 characters");

            RuleFor(x => x.SourceSystem)
                .NotEmpty().WithMessage("Source system is required")
                .MaximumLength(50).WithMessage("Source system must not exceed 50 characters");
        }
    }
}
