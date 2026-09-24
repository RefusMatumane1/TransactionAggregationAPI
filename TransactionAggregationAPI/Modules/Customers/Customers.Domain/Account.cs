using Modules.Customers.Domain.ValueObjects;
using SharedKernel.Common;
using SharedKernel.Common.ValueObjects;
using SharedKernel.Exceptions;

namespace Modules.Customers.Domain
{
    public sealed class Account : BaseEntity
    {
        private Account() { }

        public AccountId Id { get; private set; } = null!;
        public CustomerId CustomerId { get; private set; } = null!;
        public string AccountNumber { get; private set; } = null!;
        public string AccountName { get; private set; } = null!;
        public AccountType AccountType { get; private set; }
        public string Currency { get; private set; } = null!;
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
                Currency = currency.ToUpperInvariant(),
                IsActive = true
            };
        }

        public void Deactivate()
        {
            IsActive = false;
            UpdatedAt = DateTime.UtcNow;
        }
    }
}