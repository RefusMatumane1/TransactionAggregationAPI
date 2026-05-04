using TransactionAggregation.Domain.Common;
using TransactionAggregation.Domain.Common.ValueObjects;
using TransactionAggregation.Domain.Enums;
using TransactionAggregation.Domain.Exceptions;

namespace TransactionAggregation.Domain.Entities
{
    public sealed class Customer : BaseEntity
    {
        private readonly List<Transaction> _transactions = new();
        private readonly List<Account> _accounts = new();

        public CustomerId Id { get; private set; }
        public string Email { get; private set; }
        public string Name { get; private set; }
        
        public string PasswordHash { get; set; }
        public IReadOnlyCollection<Transaction> Transactions => _transactions.AsReadOnly();
        public IReadOnlyCollection<Account> Accounts => _accounts.AsReadOnly();

        private Customer() { }

        public static Customer Create(CustomerId id, string email, string name, string passwordHash)
        {
            var customer = new Customer
            {
                Id = id,
                Email = email,
                Name = name,
                PasswordHash = passwordHash
            };

           // customer.AddDomainEvent(new CustomerCreatedEvent(customer));
            return customer;
        }

        public void Update(string newEmail, string name)
        {
            Name = name;
            Email = newEmail;
            UpdatedAt = DateTime.UtcNow;

           // AddDomainEvent(new CustomerUpdatedEvent(Id, email, name));
        }

        public void AddTransaction(Transaction transaction)
        {
            if (transaction.CustomerId != Id)
                throw new DomainException("Transaction does not belong to this customer");

            _transactions.Add(transaction);
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
