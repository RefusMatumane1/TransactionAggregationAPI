using System;
using System.Collections.Generic;
using System.Text;
using TransactionAggregation.Domain.Common;
using TransactionAggregation.Domain.Common.ValueObjects;
using TransactionAggregation.Domain.Exceptions;

namespace TransactionAggregation.Domain.Entities
{
    public sealed class Customer : BaseEntity
    {
        private readonly List<Transaction> _transactions = new();

        public CustomerId Id { get; private set; }
        public string Email { get; private set; }
        public string Name { get; private set; }
        public IReadOnlyCollection<Transaction> Transactions => _transactions.AsReadOnly();

        private Customer() { }

        public static Customer Create(CustomerId id, string email, string name)
        {
            var customer = new Customer
            {
                Id = id,
                Email = email,
                Name = name
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
           // AddDomainEvent(new TransactionAddedToCustomerEvent(Id, transaction.Id));
        }
    }
}
