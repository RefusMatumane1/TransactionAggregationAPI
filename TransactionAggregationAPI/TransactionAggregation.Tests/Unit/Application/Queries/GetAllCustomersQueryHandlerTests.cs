using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using TransactionAggregation.Application.Queries.Customer.GetCustomer;
using TransactionAggregation.Domain.Common.ValueObjects;
using TransactionAggregation.Domain.Entities;
using TransactionAggregation.Tests.Helpers;
using Xunit;

namespace TransactionAggregation.Tests.Unit.Application.Queries;

public class GetAllCustomersQueryHandlerTests
{
    private static Customer MakeCustomer(string email, string name)
        => Customer.Create(CustomerId.Create(), email, name, "hashed");

    [Fact]
    public async Task Handle_ReturnsAllCustomers_WithCorrectTotalCount()
    {
        var context = InMemoryDbContextFactory.Create();
        context.Customers.AddRange(
            MakeCustomer("alice@example.com", "Alice"),
            MakeCustomer("bob@example.com", "Bob"),
            MakeCustomer("carol@example.com", "Carol"));
        await context.SaveChangesAsync();

        var handler = new GetAllCustomersQueryHandler(
            context, NullLogger<GetAllCustomersQueryHandler>.Instance);

        var result = await handler.Handle(
            new GetAllCustomersQuery(Page: 1, PageSize: 10), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalCount.Should().Be(3);
        result.Value.Items.Should().HaveCount(3);
    }

    [Fact]
    public async Task Handle_PaginationLimitsReturnedItems()
    {
        var context = InMemoryDbContextFactory.Create();
        for (int i = 1; i <= 5; i++)
            context.Customers.Add(MakeCustomer($"user{i}@example.com", $"User {i}"));
        await context.SaveChangesAsync();

        var handler = new GetAllCustomersQueryHandler(
            context, NullLogger<GetAllCustomersQueryHandler>.Instance);

        var result = await handler.Handle(
            new GetAllCustomersQuery(Page: 1, PageSize: 2), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalCount.Should().Be(5);
        result.Value.Items.Should().HaveCount(2);
        result.Value.TotalPages.Should().Be(3);
    }

    [Fact]
    public async Task Handle_SearchByName_FiltersCorrectly()
    {
        var context = InMemoryDbContextFactory.Create();
        context.Customers.AddRange(
            MakeCustomer("alice@example.com", "Alice Smith"),
            MakeCustomer("bob@example.com", "Bob Jones"),
            MakeCustomer("alicia@example.com", "Alicia Brown"));
        await context.SaveChangesAsync();

        var handler = new GetAllCustomersQueryHandler(
            context, NullLogger<GetAllCustomersQueryHandler>.Instance);

        var result = await handler.Handle(
            new GetAllCustomersQuery(SearchTerm: "alic"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(2);
        result.Value.Items.Select(c => c.Name)
              .Should().Contain("Alice Smith").And.Contain("Alicia Brown");
    }

    [Fact]
    public async Task Handle_SearchByEmail_FiltersCorrectly()
    {
        var context = InMemoryDbContextFactory.Create();
        context.Customers.AddRange(
            MakeCustomer("alice@gmail.com", "Alice"),
            MakeCustomer("bob@company.com", "Bob"));
        await context.SaveChangesAsync();

        var handler = new GetAllCustomersQueryHandler(
            context, NullLogger<GetAllCustomersQueryHandler>.Instance);

        var result = await handler.Handle(
            new GetAllCustomersQuery(SearchTerm: "gmail"), CancellationToken.None);

        result.Value.Items.Should().HaveCount(1);
        result.Value.Items.Single().Name.Should().Be("Alice");
    }

    [Fact]
    public async Task Handle_EmptyDatabase_ReturnsEmptyPage()
    {
        var context = InMemoryDbContextFactory.Create();
        var handler = new GetAllCustomersQueryHandler(
            context, NullLogger<GetAllCustomersQueryHandler>.Instance);

        var result = await handler.Handle(
            new GetAllCustomersQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalCount.Should().Be(0);
        result.Value.Items.Should().BeEmpty();
    }
}
