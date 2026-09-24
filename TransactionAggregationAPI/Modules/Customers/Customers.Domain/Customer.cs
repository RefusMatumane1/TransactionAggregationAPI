using Modules.Customers.Domain.ValueObjects;
using SharedKernel.Common;
using SharedKernel.Common.ValueObjects;
using SharedKernel.Exceptions;

namespace Modules.Customers.Domain
{
    public sealed class Customer : BaseEntity
    {
        private readonly List<Account> _accounts = new();

        public CustomerId Id { get; private set; } = null!;
        public string Email { get; private set; } = null!;
        public string Name { get; private set; } = null!;

        public IReadOnlyCollection<Account> Accounts => _accounts.AsReadOnly();

        private Customer() { }

        public static Customer Create(CustomerId id, string email, string name)
        {
            var customer = new Customer
            {
                Id = id,
                Email = email,
                Name = name
            };

            return customer;
        }

        public void Update(string newEmail, string name)
        {
            Name = name;
            Email = newEmail;
            UpdatedAt = DateTime.UtcNow;

        }

        public Account AddAccount(string accountNumber, string accountName, AccountType accountType, string currency = "ZAR")
        {
            if (_accounts.Any(a => a.AccountNumber == accountNumber))
                throw new DomainException($"Account with number '{accountNumber}' already exists for this customer");

            var account = Account.Create(Id, accountNumber, accountName, accountType, currency);
            _accounts.Add(account);
            return account;
        }
    }
}