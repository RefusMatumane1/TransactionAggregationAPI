using FluentAssertions;
using TransactionAggregation.Domain.Common.ValueObjects;
using TransactionAggregation.Domain.Entities;
using TransactionAggregation.Domain.Enums;
using TransactionAggregation.Domain.Exceptions;
using Xunit;

namespace TransactionAggregation.Tests.Unit.Domain;

public class CustomerEntityTests
{
    private static Customer MakeCustomer(string email = "test@example.com", string name = "Test User", string passwordHash = "hashedpassword")
    {
        return Customer.Create(CustomerId.Create(), email, name, passwordHash);
    }

    private static Transaction MakeTransaction(CustomerId customerId, decimal amount = -100m)
    {
        return Transaction.Create(
            customerId,
            Money.Create(amount, "ZAR"),
            "test description",
            TransactionCategory.Uncategorized,
            TransactionSource.Create("TestSource", Guid.NewGuid().ToString()));
    }

    // ── Create ────────────────────────────────────────────────────────────────

    [Fact]
    public void Create_SetsAllProperties()
    {
        var id = CustomerId.Create();
        var customer = Customer.Create(id, "user@example.com", "Alice", "hashedpassword");

        customer.Id.Should().Be(id);
        customer.Email.Should().Be("user@example.com");
        customer.Name.Should().Be("Alice");
        customer.Transactions.Should().BeEmpty();
    }

    // ── Update ────────────────────────────────────────────────────────────────

    [Fact]
    public void Update_ChangesEmailAndName()
    {
        var customer = MakeCustomer("old@example.com", "Old Name");

        customer.Update("new@example.com", "New Name");

        customer.Email.Should().Be("new@example.com");
        customer.Name.Should().Be("New Name");
        customer.UpdatedAt.Should().NotBeNull();
    }

    // ── AddTransaction ────────────────────────────────────────────────────────

    [Fact]
    public void AddTransaction_WithMatchingCustomerId_AddsSuccessfully()
    {
        var customer = MakeCustomer();
        var transaction = MakeTransaction(customer.Id);

        customer.AddTransaction(transaction);

        customer.Transactions.Should().ContainSingle();
    }

    [Fact]
    public void AddTransaction_WithWrongCustomerId_ThrowsDomainException()
    {
        var customer = MakeCustomer();
        var differentCustomerId = CustomerId.Create();
        var transaction = MakeTransaction(differentCustomerId);

        customer.Invoking(c => c.AddTransaction(transaction))
                .Should().Throw<DomainException>();
    }

    [Fact]
    public void AddMultipleTransactions_AllAreTracked()
    {
        var customer = MakeCustomer();

        customer.AddTransaction(MakeTransaction(customer.Id, -50m));
        customer.AddTransaction(MakeTransaction(customer.Id, -75m));
        customer.AddTransaction(MakeTransaction(customer.Id, 1000m));

        customer.Transactions.Should().HaveCount(3);
    }
}
