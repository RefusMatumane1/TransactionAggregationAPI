using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TransactionAggregation.Application.Commands.BankLink.InitiateBankLink;
using SharedKernel.Common.Enums;
using SharedKernel.Common.Interfaces;
using TransactionAggregation.Application.Common.Interfaces;
using TransactionAggregation.Domain.Common.ValueObjects;
using TransactionAggregation.Domain.Entities;
using TransactionAggregation.Domain.Enums;
using TransactionAggregation.Persistence;
using TransactionAggregation.Tests.Helpers;
using Xunit;

namespace TransactionAggregation.Tests.Unit.Application.Commands;

public class InitiateBankLinkCommandHandlerTests
{
    private static InitiateBankLinkCommandHandler BuildHandler(
        ApplicationDbContext ctx, IBankAggregatorClient client, IDistributedCache cache)
        => new(ctx, client, cache, NullLogger<InitiateBankLinkCommandHandler>.Instance);

    private static IBankAggregatorClient BuildClient(string authorizationUrl = "https://aggregator.example/authorize?state=x")
    {
        var client = Substitute.For<IBankAggregatorClient>();
        client.BuildAuthorizationUrl(Arg.Any<Institution>(), Arg.Any<string>()).Returns(authorizationUrl);
        return client;
    }

    private static async Task<Customer> SeedCustomerAsync(ApplicationDbContext ctx, string email = "user@example.com")
    {
        var customer = Customer.Create(CustomerId.Create(), email, "Test User");
        ctx.Customers.Add(customer);
        await ctx.SaveChangesAsync();
        return customer;
    }

    [Fact]
    public async Task Handle_NewInstitution_ReturnsAuthorizationUrlFromClient()
    {
        var context = InMemoryDbContextFactory.Create();
        var customer = await SeedCustomerAsync(context);
        var client = BuildClient("https://aggregator.example/authorize?state=abc");
        var handler = BuildHandler(context, client, new FakeDistributedCache());

        var result = await handler.Handle(
            new InitiateBankLinkCommand(customer.Id.Value, Institution.FNB), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("https://aggregator.example/authorize?state=abc");
    }

    [Fact]
    public async Task Handle_NewInstitution_PersistsPendingBankLink()
    {
        var context = InMemoryDbContextFactory.Create();
        var customer = await SeedCustomerAsync(context);
        var handler = BuildHandler(context, BuildClient(), new FakeDistributedCache());

        await handler.Handle(new InitiateBankLinkCommand(customer.Id.Value, Institution.Capitec), CancellationToken.None);

        var stored = context.BankLinks.Single();
        stored.CustomerId.Should().Be(customer.Id);
        stored.Institution.Should().Be(Institution.Capitec);
        stored.Status.Should().Be(BankLinkStatus.PendingAuthorization);
    }

    [Fact]
    public async Task Handle_NewInstitution_CachesStatePayloadMatchingCustomerAndInstitution()
    {
        var context = InMemoryDbContextFactory.Create();
        var customer = await SeedCustomerAsync(context);
        var cache = new FakeDistributedCache();
        string? capturedState = null;
        var client = Substitute.For<IBankAggregatorClient>();
        client.BuildAuthorizationUrl(Arg.Any<Institution>(), Arg.Any<string>())
            .Returns(ci =>
            {
                capturedState = ci.ArgAt<string>(1);
                return "https://aggregator.example/authorize";
            });
        var handler = BuildHandler(context, client, cache);

        await handler.Handle(new InitiateBankLinkCommand(customer.Id.Value, Institution.StandardBank), CancellationToken.None);

        capturedState.Should().NotBeNullOrEmpty();
        var cachedJson = await cache.GetStringAsync(InitiateBankLinkCommandHandler.StateCacheKey(capturedState!));
        cachedJson.Should().NotBeNull();

        var payload = JsonSerializer.Deserialize<InitiateBankLinkCommandHandler.StatePayload>(cachedJson!)!;
        payload.CustomerId.Should().Be(customer.Id.Value);
        payload.Institution.Should().Be(Institution.StandardBank);
    }

    [Fact]
    public async Task Handle_InstitutionAlreadyActive_ReturnsConflict()
    {
        var context = InMemoryDbContextFactory.Create();
        var customer = await SeedCustomerAsync(context);
        var link = BankLink.Create(customer.Id, Institution.FNB);
        link.Activate(AccountId.Create(), "ext-1", "enc-a", "enc-r", DateTime.UtcNow.AddHours(1));
        context.BankLinks.Add(link);
        await context.SaveChangesAsync();
        var handler = BuildHandler(context, BuildClient(), new FakeDistributedCache());

        var result = await handler.Handle(new InitiateBankLinkCommand(customer.Id.Value, Institution.FNB), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public async Task Handle_InstitutionAlreadyPending_ReturnsConflict()
    {
        var context = InMemoryDbContextFactory.Create();
        var customer = await SeedCustomerAsync(context);
        context.BankLinks.Add(BankLink.Create(customer.Id, Institution.FNB));
        await context.SaveChangesAsync();
        var handler = BuildHandler(context, BuildClient(), new FakeDistributedCache());

        var result = await handler.Handle(new InitiateBankLinkCommand(customer.Id.Value, Institution.FNB), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Conflict);
    }

    [Theory]
    [InlineData(BankLinkStatus.Revoked)]
    [InlineData(BankLinkStatus.NeedsReauthorization)]
    public async Task Handle_ReauthorizableExistingLink_ResetsToPendingWithoutDuplicatingRow(BankLinkStatus initialStatus)
    {
        var context = InMemoryDbContextFactory.Create();
        var customer = await SeedCustomerAsync(context);
        var link = BankLink.Create(customer.Id, Institution.FNB);
        link.Activate(AccountId.Create(), "ext-1", "enc-a", "enc-r", DateTime.UtcNow.AddHours(1));
        if (initialStatus == BankLinkStatus.Revoked)
            link.Revoke();
        else
            link.MarkNeedsReauthorization();
        context.BankLinks.Add(link);
        await context.SaveChangesAsync();
        var handler = BuildHandler(context, BuildClient(), new FakeDistributedCache());

        var result = await handler.Handle(new InitiateBankLinkCommand(customer.Id.Value, Institution.FNB), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        context.BankLinks.Should().HaveCount(1);
        var stored = context.BankLinks.Single();
        stored.Id.Should().Be(link.Id);
        stored.Status.Should().Be(BankLinkStatus.PendingAuthorization);
    }

    [Fact]
    public async Task Handle_DifferentInstitutionForSameCustomer_Succeeds()
    {
        var context = InMemoryDbContextFactory.Create();
        var customer = await SeedCustomerAsync(context);
        context.BankLinks.Add(BankLink.Create(customer.Id, Institution.FNB));
        await context.SaveChangesAsync();
        var handler = BuildHandler(context, BuildClient(), new FakeDistributedCache());

        var result = await handler.Handle(new InitiateBankLinkCommand(customer.Id.Value, Institution.Absa), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        context.BankLinks.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_AggregatorClientThrows_PropagatesToCentralizedExceptionHandling()
    {
        // Unexpected exceptions are no longer swallowed into a generic Result.Unexpected
        // by the handler — they propagate so the centralized exception-handling
        // middleware (and its trace-ID-bearing ProblemDetails response) handles them.
        var context = InMemoryDbContextFactory.Create();
        var customer = await SeedCustomerAsync(context);
        var client = Substitute.For<IBankAggregatorClient>();
        client.BuildAuthorizationUrl(Arg.Any<Institution>(), Arg.Any<string>())
            .Returns(_ => throw new InvalidOperationException("aggregator unreachable"));
        var handler = BuildHandler(context, client, new FakeDistributedCache());

        var act = async () => await handler.Handle(new InitiateBankLinkCommand(customer.Id.Value, Institution.FNB), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("aggregator unreachable");
    }
}