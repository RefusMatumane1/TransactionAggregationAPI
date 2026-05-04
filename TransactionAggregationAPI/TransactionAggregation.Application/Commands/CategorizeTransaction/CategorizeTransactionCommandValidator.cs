using FluentValidation;
using TransactionAggregation.Domain.Enums;

namespace TransactionAggregation.Application.Commands.CategorizeTransaction
{
    public sealed class CategorizeTransactionCommandValidator : AbstractValidator<CategorizeTransactionCommand>
    {
        public CategorizeTransactionCommandValidator()
        {
            RuleFor(x => x.TransactionId)
                .NotEmpty().WithMessage("Transaction ID is required");

            RuleFor(x => x.Category)
                .NotEqual(TransactionCategory.Uncategorized)
                .WithMessage("Category must be a valid category, not Uncategorized");
        }
    }
}
