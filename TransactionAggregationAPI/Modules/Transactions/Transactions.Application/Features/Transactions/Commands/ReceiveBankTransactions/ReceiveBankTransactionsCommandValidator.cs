using FluentValidation;
using Modules.Transactions.Application.Common.DTOs;

namespace Modules.Transactions.Application.Features.Transactions.Commands.ReceiveBankTransactions
{
    public sealed class ReceiveBankTransactionsCommandValidator : AbstractValidator<ReceiveBankTransactionsCommand>
    {
        /// <summary>Matches the numeric(19,4) column: anything finer would be silently rounded.</summary>
        public const int MaxAmountScale = 4;

        public ReceiveBankTransactionsCommandValidator()
        {
            RuleFor(x => x.SourceName).NotEmpty();
            RuleFor(x => x.SchemaVersion)
                .Must(BankTransactionsMessage.SupportedSchemaVersions.Contains)
                .WithMessage(x => $"Unsupported schemaVersion {x.SchemaVersion}; supported: {string.Join(", ", BankTransactionsMessage.SupportedSchemaVersions)}.");
            RuleFor(x => x.ExternalAccountId).NotEmpty();
            RuleFor(x => x.IdempotencyKey).MaximumLength(200);
            RuleFor(x => x.Transactions).NotEmpty();
            RuleFor(x => x.Transactions)
                .Must(t => t.Count <= 500)
                .WithMessage("A single webhook call may contain at most 500 transactions — split larger deliveries into multiple calls.");

            RuleForEach(x => x.Transactions).ChildRules(transaction =>
            {
                transaction.RuleFor(t => t.Id).NotEmpty();
                transaction.RuleFor(t => t.Amount).NotEqual(0);
                transaction.RuleFor(t => t.Currency).NotEmpty().Matches("^[A-Za-z]{3}$")
                    .WithMessage("Currency must be a 3-letter ISO 4217 code.");
                transaction.RuleFor(t => t.Amount)
                    .Must(a => decimal.Round(a, MaxAmountScale) == a)
                    .WithMessage($"Amount may carry at most {MaxAmountScale} decimal places.");
                transaction.RuleFor(t => t.Description).NotEmpty();
                transaction.RuleFor(t => t.Date).NotEqual(default(DateTime));
                transaction.RuleFor(t => t.Status)
                    .Must(BankTransactionStatus.IsValid)
                    .WithMessage($"Status must be '{BankTransactionStatus.Pending}' or '{BankTransactionStatus.Posted}' (or omitted, meaning posted).");
            });
        }
    }
}