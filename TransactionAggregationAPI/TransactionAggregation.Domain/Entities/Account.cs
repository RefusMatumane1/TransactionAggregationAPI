using TransactionAggregation.Domain.Common;
using TransactionAggregation.Domain.Common.ValueObjects;
using TransactionAggregation.Domain.Enums;
using TransactionAggregation.Domain.Exceptions;

namespace TransactionAggregation.Domain.Entities
{
    public sealed class Account : BaseEntity
    {
        private readonly List<Transaction> _transactions = new();

        private Account() { }

        public AccountId Id { get; private set; }
        public CustomerId CustomerId { get; private set; }
        public IReadOnlyCollection<Transaction> Transactions => _transactions.AsReadOnly();
        public string AccountNumber { get; private set; }
        public string AccountName { get; private set; }
        public AccountType AccountType { get; private set; }
        public decimal Balance { get; private set; }
        public string Currency { get; private set; }
        public bool IsActive { get; private set; }

        public static Account Create(
            CustomerId customerId,
            string accountNumber,
            string accountName,
            AccountType accountType,
            string currency = "ZAR")
        {
            if (string.IsNullOrWhiteSpace(accountNumber))
                throw new DomainException("Account number is required");

            if (string.IsNullOrWhiteSpace(accountName))
                throw new DomainException("Account name is required");

            if (string.IsNullOrWhiteSpace(currency) || currency.Length != 3)
                throw new DomainException("Currency must be a valid ISO 4217 code");

            return new Account
            {
                Id = AccountId.Create(),
                CustomerId = customerId,
                AccountNumber = accountNumber.Trim(),
                AccountName = accountName.Trim(),
                AccountType = accountType,
                Balance = 0m,
                Currency = currency.ToUpperInvariant(),
                IsActive = true
            };
        }

        public void Credit(decimal amount)
        {
            if (amount <= 0)
                throw new DomainException("Credit amount must be positive");

            if (!IsActive)
                throw new DomainException("Cannot credit an inactive account");

            Balance += amount;
            UpdatedAt = DateTime.UtcNow;
        }

        public void Debit(decimal amount)
        {
            if (amount <= 0)
                throw new DomainException("Debit amount must be positive");

            if (!IsActive)
                throw new DomainException("Cannot debit an inactive account");

            Balance -= amount;
            UpdatedAt = DateTime.UtcNow;
        }

        public void Deactivate()
        {
            IsActive = false;
            UpdatedAt = DateTime.UtcNow;
        }

        public void Reactivate()
        {
            IsActive = true;
            UpdatedAt = DateTime.UtcNow;
        }
    }
}
