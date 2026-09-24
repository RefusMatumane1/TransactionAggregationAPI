using FluentAssertions;
using Modules.Customers.Domain;
using SharedKernel.Common.ValueObjects;
using Xunit;

namespace TransactionAggregation.Tests.Unit.Domain;

public class CustomerEntityTests
{
    private static Customer MakeCustomer(string email = "test@example.com", string name = "Test User")
    {
        return Customer.Create(CustomerId.Create(), email, name);
    }

    [Fact]
    public void Create_SetsAllProperties()
    {
        var id = CustomerId.Create();
        var customer = Customer.Create(id, "user@example.com", "Alice");

        customer.Id.Should().Be(id);
        customer.Email.Should().Be("user@example.com");
        customer.Name.Should().Be("Alice");
        customer.Accounts.Should().BeEmpty();
    }

    [Fact]
    public void Update_ChangesEmailAndName()
    {
        var customer = MakeCustomer("old@example.com", "Old Name");

        customer.Update("new@example.com", "New Name");

        customer.Email.Should().Be("new@example.com");
        customer.Name.Should().Be("New Name");
        customer.UpdatedAt.Should().NotBeNull();
    }
}