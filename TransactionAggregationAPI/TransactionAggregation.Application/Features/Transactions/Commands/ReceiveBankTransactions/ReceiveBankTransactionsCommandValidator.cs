using FluentValidation;

namespace TransactionAggregation.Application.Features.Transactions.Commands.ReceiveBankTransactions
{
    public sealed class ReceiveBankTransactionsCommandValidator : AbstractValidator<ReceiveBankTransactionsCommand>
    {
        public ReceiveBankTransactionsCommandValidator()
        {
            RuleFor(x => x.SourceName).NotEmpty();
            RuleFor(x => x.ExternalAccountId).NotEmpty();
            RuleFor(x => x.Transactions).NotEmpty();
            RuleFor(x => x.Transactions)
                .Must(t => t.Count <= 500)
                .WithMessage("A single webhook call may contain at most 500 transactions — split larger deliveries into multiple calls.");

            RuleForEach(x => x.Transactions).ChildRules(transaction =>
            {
                transaction.RuleFor(t => t.Id).NotEmpty();
                transaction.RuleFor(t => t.Amount).NotEqual(0);
                transaction.RuleFor(t => t.Currency).NotEmpty().Length(3);
                transaction.RuleFor(t => t.Description).NotEmpty();
                transaction.RuleFor(t => t.Date).NotEqual(default(DateTime));
            });
        }
    }
}
