using System;
using System.Collections.Generic;
using System.Text;
using TransactionAggregation.Domain.Exceptions;

namespace TransactionAggregation.Domain.Common.ValueObjects
{
    public sealed class Money : ValueObject
    {
        public decimal Amount { get; }
        public string Currency { get; }
        public string Formatted => $"{Currency} {Amount:N2}";

        private Money(decimal amount, string currency)
        {
            if (amount == 0)
                throw new DomainException("Amount cannot be zero");

            if (string.IsNullOrWhiteSpace(currency) || currency.Length != 3)
                throw new DomainException("Currency must be a valid ISO 4217 code");

            Amount = amount;
            Currency = currency.ToUpperInvariant();
        }

        public static Money Create(decimal amount, string currency = "ZAR")
        {
            return new Money(amount, currency);
        }

        public bool IsIncome => Amount > 0;
        public bool IsExpense => Amount < 0;
        public decimal AbsoluteAmount => Math.Abs(Amount);

        protected override IEnumerable<object> GetEqualityComponents()
        {
            yield return Amount;
            yield return Currency;
        }
    }
}
