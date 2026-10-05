using SharedKernel.Common.ValueObjects;
using SharedKernel.Exceptions;

namespace Modules.Transactions.Domain.Common.ValueObjects
{
    public sealed class Money : ValueObject
    {
        public decimal Amount { get; }
        public string Currency { get; }

        public const int MaxScale = 4;
        public const decimal MaxAbsoluteAmount = 999_999_999_999_999.9999m;

        private Money(decimal amount, string currency)
        {
            if (amount == 0)
                throw new DomainException("Amount cannot be zero");

            if (Math.Abs(amount) > MaxAbsoluteAmount)
                throw new DomainException("Amount must be less than 10^15 in absolute value");

            if (decimal.Round(amount, MaxScale) != amount)
                throw new DomainException($"Amount may carry at most {MaxScale} decimal places");

            if (!SupportedCurrency.IsSupported(currency))
                throw new DomainException($"'{currency}' is not an ISO 4217 currency code");

            Amount = amount;
            Currency = SupportedCurrency.Normalize(currency);
        }

        public static Money Create(decimal amount, string currency) => new(amount, currency);

        public bool IsIncome => Amount > 0;
        public bool IsExpense => Amount < 0;

        protected override IEnumerable<object> GetEqualityComponents()
        {
            yield return Amount;
            yield return Currency;
        }
    }
}