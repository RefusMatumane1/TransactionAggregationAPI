using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Modules.Customers.Application.Errors;
using Modules.Customers.Application.Features.GetCustomerByEmail;
using Modules.Customers.Domain;
using SharedKernel.Common.ValueObjects;
using TransactionAggregation.Tests.Helpers;
using Xunit;

namespace TransactionAggregation.Tests.Unit.Application.Queries;

public class GetCustomerByEmailQueryHandlerTests
{
    private const string Email = "owner@example.com";

    private static async Task<(GetCustomerByEmailQueryHandler Handler, Customer Customer)> SeedAsync()
    {
        var context = InMemoryCustomersDbContextFactory.Create();
        var customer = Customer.Create(CustomerId.Create(), Email, "Owner");
        context.Customers.Add(customer);
        await context.SaveChangesAsync();
        return (new GetCustomerByEmailQueryHandler(context, NullLogger<GetCustomerByEmailQueryHandler>.Instance), customer);
    }

    [Fact]
    public async Task Handle_OwnEmail_ReturnsTheCustomer()
    {
        var (handler, customer) = await SeedAsync();

        var result = await handler.Handle(new GetCustomerByEmailQuery(Email, customer.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(customer.Id.Value);
    }

    [Fact]
    public async Task Handle_AnotherCustomersEmail_FailsExactlyLikeAnUnregisteredEmail()
    {
        var (handler, _) = await SeedAsync();
        var otherCaller = Guid.NewGuid();

        var registered = await handler.Handle(new GetCustomerByEmailQuery(Email, otherCaller), CancellationToken.None);
        var unregistered = await handler.Handle(new GetCustomerByEmailQuery("nobody@example.com", otherCaller), CancellationToken.None);

        registered.Error.Should().Be(CustomerErrors.NotFoundByEmail());
        unregistered.Error.Should().Be(CustomerErrors.NotFoundByEmail());
    }
}