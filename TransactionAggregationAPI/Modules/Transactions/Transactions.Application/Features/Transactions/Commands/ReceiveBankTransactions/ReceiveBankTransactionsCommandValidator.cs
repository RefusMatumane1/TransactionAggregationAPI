using FluentValidation;
using Modules.Transactions.Application.Common.DTOs;
using Modules.Transactions.Domain.Common.ValueObjects;
using Modules.Transactions.Domain.Entities;
using SharedKernel.Common.ValueObjects;

namespace Modules.Transactions.Application.Features.Transactions.Commands.ReceiveBankTransactions
{
    public sealed class ReceiveBankTransactionsCommandValidator : AbstractValidator<ReceiveBankTransactionsCommand>
    {
        public const int MaxAmountScale = Money.MaxScale;

        public const int MaxTransactionsPerDelivery = 500;
        public const int MaxExternalAccountIdLength = Transaction.MaxExternalAccountIdLength;
        public const int MaxIdempotencyKeyLength = 200;
        public const int MaxCategoryLength = 100;

        public ReceiveBankTransactionsCommandValidator()
        {
            RuleFor(x => x.SourceName).NotEmpty();
            RuleFor(x => x.SchemaVersion)
                .Must(BankTransactionsMessage.SupportedSchemaVersions.Contains)
                .WithMessage(x => $"Unsupported schemaVersion {x.SchemaVersion}; supported: {string.Join(", ", BankTransactionsMessage.SupportedSchemaVersions)}.");
            RuleFor(x => x.ExternalAccountId).NotEmpty().MaximumLength(MaxExternalAccountIdLength).Must(HaveNoControlCharacters)
                .WithMessage(ControlCharacterMessage);
            RuleFor(x => x.Institution).MaximumLength(TransactionSource.MaxNameLength).Must(HaveNoControlCharacters)
                .WithMessage(ControlCharacterMessage);
            RuleFor(x => x.IdempotencyKey).MaximumLength(MaxIdempotencyKeyLength).Must(HaveNoControlCharacters)
                .WithMessage(ControlCharacterMessage);
            RuleFor(x => x.Transactions).NotEmpty();
            RuleFor(x => x.Transactions)
                .Must(t => t.Count <= MaxTransactionsPerDelivery)
                .WithMessage($"A single webhook call may contain at most {MaxTransactionsPerDelivery} transactions — split larger deliveries into multiple calls.");

            RuleForEach(x => x.Transactions).ChildRules(transaction =>
            {
                transaction.RuleFor(t => t.Id).NotEmpty().MaximumLength(Transaction.MaxExternalIdLength)
                    .Must(HaveNoControlCharacters).WithMessage(ControlCharacterMessage);
                transaction.RuleFor(t => t.Amount).NotEqual(0);
                transaction.RuleFor(t => t.Amount)
                    .Must(a => Math.Abs(a) <= Money.MaxAbsoluteAmount)
                    .WithMessage($"Amount must be less than 10^15 in absolute value.");
                transaction.RuleFor(t => t.Amount)
                    .Must(a => decimal.Round(a, MaxAmountScale) == a)
                    .WithMessage($"Amount may carry at most {MaxAmountScale} decimal places.");
                transaction.RuleFor(t => t.Currency).Must(SupportedCurrency.IsSupported)
                    .WithMessage("Currency must be an ISO 4217 currency code, e.g. ZAR.");
                transaction.RuleFor(t => t.Description).NotEmpty().MaximumLength(Transaction.MaxDescriptionLength)
                    .Must(HaveNoControlCharacters).WithMessage(ControlCharacterMessage);
                transaction.RuleFor(t => t.Category).MaximumLength(MaxCategoryLength)
                    .Must(HaveNoControlCharacters).WithMessage(ControlCharacterMessage);
                transaction.RuleFor(t => t.Date).NotEqual(default(DateTime));
                transaction.RuleFor(t => t.Status)
                    .Must(BankTransactionStatus.IsValid)
                    .WithMessage($"Status must be '{BankTransactionStatus.Pending}' or '{BankTransactionStatus.Posted}' (or omitted, meaning posted).");
            });
        }

        private const string ControlCharacterMessage = "Must not contain control characters.";

        private static bool HaveNoControlCharacters(string? value) =>
            value is null || !value.Any(char.IsControl);
    }
}