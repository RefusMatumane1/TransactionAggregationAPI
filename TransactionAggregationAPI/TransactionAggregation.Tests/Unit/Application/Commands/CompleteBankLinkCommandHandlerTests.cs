using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TransactionAggregation.Application.Commands.BankLink.CompleteBankLink;
using TransactionAggregation.Application.Commands.BankLink.InitiateBankLink;
using TransactionAggregation.Application.Common.DTOs;
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

public class CompleteBankLinkCommandHandlerTests
{
    private const string Code = "auth-code-123";

    private static CompleteBankLinkCommandHandler BuildHandler(
        ApplicationDbContext ctx,
        IBankAggregatorClient client,
        IDistributedCache cache,
        IBankLinkCredentialProtector? protector = null)
        => new(ctx, client, protector ?? BuildProtector(), cache, NullLogger<CompleteBankLinkCommandHandler>.Instance);

    private static IBankLinkCredentialProtector BuildProtector()
    {
        var protector = Substitute.For<IBankLinkCredentialProtector>();
        protector.Protect(Arg.Any<string>()).Returns(ci => $"enc:{ci.ArgAt<string>(0)}");
        return protector;
    }

    private static IBankAggregatorClient BuildClient(
        AggregatorLinkedAccountResult? linkedAccount = null,
        AggregatorTokenResult? tokens = null)
    {
        var client = Substitute.For<IBankAggregatorClient>();
        client.ExchangeAuthorizationCodeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(tokens ?? new AggregatorTokenResult("access-token", "refresh-token", DateTime.UtcNow.AddHours(1)));
        client.GetLinkedAccountAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(linkedAccount ?? new AggregatorLinkedAccountResult("ext-acc-1", "ACC-001", "Cheque Account", "checking", "ZAR"));
        return client;
    }

    private static async Task<Customer> SeedCustomerAsync(ApplicationDbContext ctx, string email = "user@example.com")
    {
        var customer = Customer.Create(CustomerId.Create(), email, "Test User");
        ctx.Customers.Add(customer);
        await ctx.SaveChangesAsync();
        return customer;
    }

    private static async Task<string> SeedPendingStateAsync(
        FakeDistributedCache cache, ApplicationDbContext ctx, CustomerId customerId, Institution institution, string state = "state-token")
    {
        ctx.BankLinks.Add(BankLink.Create(customerId, institution));
        await ctx.SaveChangesAsync();

        var payload = JsonSerializer.Serialize(new InitiateBankLinkCommandHandler.StatePayload(customerId.Value, institution));
        await cache.SetStringAsync(
            InitiateBankLinkCommandHandler.StateCacheKey(state),
            payload,
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10) });

        return state;
    }

    [Fact]
    public async Task Handle_ValidStateAndCode_ActivatesLinkAndReturnsAccountId()
    {
        var context = InMemoryDbContextFactory.Create();
        var customer = await SeedCustomerAsync(context);
        var cache = new FakeDistributedCache();
        var state = await SeedPendingStateAsync(cache, context, customer.Id, Institution.FNB);
        var handler = BuildHandler(context, BuildClient(), cache);

        var result = await handler.Handle(new CompleteBankLinkCommand(Code, state), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var link = context.BankLinks.Single();
        link.Status.Should().Be(BankLinkStatus.Active);
        link.AccountId!.Value.Should().Be(result.Value);
    }

    [Fact]
    public async Task Handle_ValidStateAndCode_CreatesAccountForCustomer()
    {
        var context = InMemoryDbContextFactory.Create();
        var customer = await SeedCustomerAsync(context);
        var cache = new FakeDistributedCache();
        var state = await SeedPendingStateAsync(cache, context, customer.Id, Institution.FNB);
        var handler = BuildHandler(
            context,
            BuildClient(linkedAccount: new AggregatorLinkedAccountResult("ext-9", "ACC-777", "Savings", "savings", "ZAR")),
            cache);

        await handler.Handle(new CompleteBankLinkCommand(Code, state), CancellationToken.None);

        var storedCustomer = context.Customers.Include(c => c.Accounts).Single(c => c.Id == customer.Id);
        storedCustomer.Accounts.Should().ContainSingle();
        storedCustomer.Accounts.Single().AccountNumber.Should().Be("ACC-777");
        storedCustomer.Accounts.Single().AccountType.Should().Be(AccountType.Savings);
    }

    [Fact]
    public async Task Handle_ValidStateAndCode_EncryptsTokensBeforePersisting()
    {
        var context = InMemoryDbContextFactory.Create();
        var customer = await SeedCustomerAsync(context);
        var cache = new FakeDistributedCache();
        var state = await SeedPendingStateAsync(cache, context, customer.Id, Institution.FNB);
        var handler = BuildHandler(
            context,
            BuildClient(tokens: new AggregatorTokenResult("my-access", "my-refresh", DateTime.UtcNow.AddHours(1))),
            cache);

        await handler.Handle(new CompleteBankLinkCommand(Code, state), CancellationToken.None);

        var link = context.BankLinks.Single();
        link.EncryptedAccessToken.Should().Be("enc:my-access");
        link.EncryptedRefreshToken.Should().Be("enc:my-refresh");
    }

    [Fact]
    public async Task Handle_ValidState_IsOneTimeUse_RemovedFromCacheAfterCompletion()
    {
        var context = InMemoryDbContextFactory.Create();
        var customer = await SeedCustomerAsync(context);
        var cache = new FakeDistributedCache();
        var state = await SeedPendingStateAsync(cache, context, customer.Id, Institution.FNB);
        var handler = BuildHandler(context, BuildClient(), cache);

        var first = await handler.Handle(new CompleteBankLinkCommand(Code, state), CancellationToken.None);
        first.IsSuccess.Should().BeTrue();

        var replay = await handler.Handle(new CompleteBankLinkCommand(Code, state), CancellationToken.None);

        replay.IsFailure.Should().BeTrue();
        replay.Error.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task Handle_UnknownState_ReturnsValidationFailure()
    {
        var context = InMemoryDbContextFactory.Create();
        var handler = BuildHandler(context, BuildClient(), new FakeDistributedCache());

        var result = await handler.Handle(new CompleteBankLinkCommand(Code, "never-issued-state"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task Handle_StateReferencesLinkThatIsNoLongerPending_ReturnsValidationFailure()
    {
        var context = InMemoryDbContextFactory.Create();
        var customer = await SeedCustomerAsync(context);
        var cache = new FakeDistributedCache();
        var state = await SeedPendingStateAsync(cache, context, customer.Id, Institution.FNB);

        var link = context.BankLinks.Single();
        link.Activate(AccountId.Create(), "ext-1", "enc-a", "enc-r", DateTime.UtcNow.AddHours(1));
        await context.SaveChangesAsync();
        var handler = BuildHandler(context, BuildClient(), cache);

        var result = await handler.Handle(new CompleteBankLinkCommand(Code, state), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task Handle_StateReferencesMissingCustomer_ReturnsNotFound()
    {
        var context = InMemoryDbContextFactory.Create();
        var cache = new FakeDistributedCache();
        var missingCustomerId = CustomerId.Create();
        context.BankLinks.Add(BankLink.Create(missingCustomerId, Institution.FNB));
        await context.SaveChangesAsync();
        var payload = JsonSerializer.Serialize(new InitiateBankLinkCommandHandler.StatePayload(missingCustomerId.Value, Institution.FNB));
        await cache.SetStringAsync(
            InitiateBankLinkCommandHandler.StateCacheKey("orphan-state"),
            payload,
            new DistributedCacheEntryOptions());
        var handler = BuildHandler(context, BuildClient(), cache);

        var result = await handler.Handle(new CompleteBankLinkCommand(Code, "orphan-state"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Handle_ReLinkingPreviouslyKnownAccountNumber_ReusesExistingAccountInsteadOfFailing()
    {
        var context = InMemoryDbContextFactory.Create();
        var customer = await SeedCustomerAsync(context);
        var existingAccount = customer.AddAccount("ACC-001", "Cheque Account", AccountType.Checking, "ZAR");
        await context.SaveChangesAsync();

        var cache = new FakeDistributedCache();
        var state = await SeedPendingStateAsync(cache, context, customer.Id, Institution.FNB);
        var handler = BuildHandler(
            context,
            BuildClient(linkedAccount: new AggregatorLinkedAccountResult("ext-1", "ACC-001", "Cheque Account", "checking", "ZAR")),
            cache);

        var result = await handler.Handle(new CompleteBankLinkCommand(Code, state), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(existingAccount.Id.Value);
        context.Customers.Include(c => c.Accounts).Single().Accounts.Should().ContainSingle();
    }

    [Theory]
    [InlineData("savings", AccountType.Savings)]
    [InlineData("credit", AccountType.CreditCard)]
    [InlineData("creditcard", AccountType.CreditCard)]
    [InlineData("credit_card", AccountType.CreditCard)]
    [InlineData("investment", AccountType.Investment)]
    [InlineData("loan", AccountType.Loan)]
    [InlineData("checking", AccountType.Checking)]
    [InlineData("something-unrecognised", AccountType.Checking)]
    public async Task Handle_MapsAggregatorAccountType(string aggregatorType, AccountType expected)
    {
        var context = InMemoryDbContextFactory.Create();
        var customer = await SeedCustomerAsync(context);
        var cache = new FakeDistributedCache();
        var state = await SeedPendingStateAsync(cache, context, customer.Id, Institution.FNB);
        var handler = BuildHandler(
            context,
            BuildClient(linkedAccount: new AggregatorLinkedAccountResult("ext-1", "ACC-001", "Account", aggregatorType, "ZAR")),
            cache);

        await handler.Handle(new CompleteBankLinkCommand(Code, state), CancellationToken.None);

        context.Customers.Include(c => c.Accounts).Single().Accounts.Single().AccountType.Should().Be(expected);
    }
}