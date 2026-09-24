using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging.Abstractions;
using Modules.BankLinks.Application.Features.CompleteBankLink;
using Modules.BankLinks.Application.Features.InitiateBankLink;
using Modules.BankLinks.Application.Persistence;
using Modules.BankLinks.Application.Ports;
using Modules.BankLinks.Domain;
using Modules.BankLinks.Domain.ValueObjects;
using Modules.Customers.Application.Adapters;
using Modules.Customers.Domain;
using Modules.Customers.Domain.ValueObjects;
using Modules.Customers.Infrastructure.Persistence;
using NSubstitute;
using SharedKernel.Common.Enums;
using SharedKernel.Common.ValueObjects;
using System.Text.Json;
using TransactionAggregation.Tests.Helpers;
using Xunit;

namespace TransactionAggregation.Tests.Unit.Application.Commands;

public class CompleteBankLinkCommandHandlerTests
{
    private const string Code = "auth-code-123";

    private static CompleteBankLinkCommandHandler BuildHandler(
        IBankLinksDbContext bankLinksCtx,
        CustomersDbContext appCtx,
        IBankAggregatorClient client,
        IDistributedCache cache,
        IBankLinkCredentialProtector? protector = null)
        => new(
            bankLinksCtx,
            client,
            protector ?? BuildProtector(),
            new AccountProvisioningAdapter(appCtx),
            cache,
            NullLogger<CompleteBankLinkCommandHandler>.Instance);

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

    private static async Task<Customer> SeedCustomerAsync(CustomersDbContext ctx, string email = "user@example.com")
    {
        var customer = Customer.Create(CustomerId.Create(), email, "Test User");
        ctx.Customers.Add(customer);
        await ctx.SaveChangesAsync();
        return customer;
    }

    private static async Task<string> SeedPendingStateAsync(
        FakeDistributedCache cache, IBankLinksDbContext ctx, CustomerId customerId, Institution institution, string state = "state-token")
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
        var appContext = InMemoryCustomersDbContextFactory.Create();
        var bankLinksContext = InMemoryBankLinksDbContextFactory.Create();
        var customer = await SeedCustomerAsync(appContext);
        var cache = new FakeDistributedCache();
        var state = await SeedPendingStateAsync(cache, bankLinksContext, customer.Id, Institution.FNB);
        var handler = BuildHandler(bankLinksContext, appContext, BuildClient(), cache);

        var result = await handler.Handle(new CompleteBankLinkCommand(Code, state), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var link = bankLinksContext.BankLinks.Single();
        link.Status.Should().Be(BankLinkStatus.Active);
        link.AccountId!.Value.Should().Be(result.Value);
    }

    [Fact]
    public async Task Handle_ValidStateAndCode_CreatesAccountForCustomer()
    {
        var appContext = InMemoryCustomersDbContextFactory.Create();
        var bankLinksContext = InMemoryBankLinksDbContextFactory.Create();
        var customer = await SeedCustomerAsync(appContext);
        var cache = new FakeDistributedCache();
        var state = await SeedPendingStateAsync(cache, bankLinksContext, customer.Id, Institution.FNB);
        var handler = BuildHandler(
            bankLinksContext,
            appContext,
            BuildClient(linkedAccount: new AggregatorLinkedAccountResult("ext-9", "ACC-777", "Savings", "savings", "ZAR")),
            cache);

        await handler.Handle(new CompleteBankLinkCommand(Code, state), CancellationToken.None);

        appContext.Accounts.Should().ContainSingle();
        appContext.Accounts.Single().AccountNumber.Should().Be("ACC-777");
        appContext.Accounts.Single().AccountType.Should().Be(AccountType.Savings);
    }

    [Fact]
    public async Task Handle_ValidStateAndCode_EncryptsTokensBeforePersisting()
    {
        var appContext = InMemoryCustomersDbContextFactory.Create();
        var bankLinksContext = InMemoryBankLinksDbContextFactory.Create();
        var customer = await SeedCustomerAsync(appContext);
        var cache = new FakeDistributedCache();
        var state = await SeedPendingStateAsync(cache, bankLinksContext, customer.Id, Institution.FNB);
        var handler = BuildHandler(
            bankLinksContext,
            appContext,
            BuildClient(tokens: new AggregatorTokenResult("my-access", "my-refresh", DateTime.UtcNow.AddHours(1))),
            cache);

        await handler.Handle(new CompleteBankLinkCommand(Code, state), CancellationToken.None);

        var link = bankLinksContext.BankLinks.Single();
        link.EncryptedAccessToken.Should().Be("enc:my-access");
        link.EncryptedRefreshToken.Should().Be("enc:my-refresh");
    }

    [Fact]
    public async Task Handle_ValidState_IsOneTimeUse_RemovedFromCacheAfterCompletion()
    {
        var appContext = InMemoryCustomersDbContextFactory.Create();
        var bankLinksContext = InMemoryBankLinksDbContextFactory.Create();
        var customer = await SeedCustomerAsync(appContext);
        var cache = new FakeDistributedCache();
        var state = await SeedPendingStateAsync(cache, bankLinksContext, customer.Id, Institution.FNB);
        var handler = BuildHandler(bankLinksContext, appContext, BuildClient(), cache);

        var first = await handler.Handle(new CompleteBankLinkCommand(Code, state), CancellationToken.None);
        first.IsSuccess.Should().BeTrue();

        var replay = await handler.Handle(new CompleteBankLinkCommand(Code, state), CancellationToken.None);

        replay.IsFailure.Should().BeTrue();
        replay.Error.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task Handle_UnknownState_ReturnsValidationFailure()
    {
        var appContext = InMemoryCustomersDbContextFactory.Create();
        var bankLinksContext = InMemoryBankLinksDbContextFactory.Create();
        var handler = BuildHandler(bankLinksContext, appContext, BuildClient(), new FakeDistributedCache());

        var result = await handler.Handle(new CompleteBankLinkCommand(Code, "never-issued-state"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task Handle_StateReferencesLinkThatIsNoLongerPending_ReturnsValidationFailure()
    {
        var appContext = InMemoryCustomersDbContextFactory.Create();
        var bankLinksContext = InMemoryBankLinksDbContextFactory.Create();
        var customer = await SeedCustomerAsync(appContext);
        var cache = new FakeDistributedCache();
        var state = await SeedPendingStateAsync(cache, bankLinksContext, customer.Id, Institution.FNB);

        var link = bankLinksContext.BankLinks.Single();
        link.Activate(AccountId.Create(), "ext-1", "enc-a", "enc-r", DateTime.UtcNow.AddHours(1));
        await bankLinksContext.SaveChangesAsync();
        var handler = BuildHandler(bankLinksContext, appContext, BuildClient(), cache);

        var result = await handler.Handle(new CompleteBankLinkCommand(Code, state), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task Handle_StateReferencesMissingCustomer_ReturnsNotFound()
    {
        var appContext = InMemoryCustomersDbContextFactory.Create();
        var bankLinksContext = InMemoryBankLinksDbContextFactory.Create();
        var cache = new FakeDistributedCache();
        var missingCustomerId = CustomerId.Create();
        bankLinksContext.BankLinks.Add(BankLink.Create(missingCustomerId, Institution.FNB));
        await bankLinksContext.SaveChangesAsync();
        var payload = JsonSerializer.Serialize(new InitiateBankLinkCommandHandler.StatePayload(missingCustomerId.Value, Institution.FNB));
        await cache.SetStringAsync(
            InitiateBankLinkCommandHandler.StateCacheKey("orphan-state"),
            payload,
            new DistributedCacheEntryOptions());
        var handler = BuildHandler(bankLinksContext, appContext, BuildClient(), cache);

        var result = await handler.Handle(new CompleteBankLinkCommand(Code, "orphan-state"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Handle_ReLinkingPreviouslyKnownAccountNumber_ReusesExistingAccountInsteadOfFailing()
    {
        var appContext = InMemoryCustomersDbContextFactory.Create();
        var bankLinksContext = InMemoryBankLinksDbContextFactory.Create();
        var customer = await SeedCustomerAsync(appContext);
        var existingAccount = Account.Create(customer.Id, "ACC-001", "Cheque Account", AccountType.Checking, "ZAR");
        appContext.Accounts.Add(existingAccount);
        await appContext.SaveChangesAsync();

        var cache = new FakeDistributedCache();
        var state = await SeedPendingStateAsync(cache, bankLinksContext, customer.Id, Institution.FNB);
        var handler = BuildHandler(
            bankLinksContext,
            appContext,
            BuildClient(linkedAccount: new AggregatorLinkedAccountResult("ext-1", "ACC-001", "Cheque Account", "checking", "ZAR")),
            cache);

        var result = await handler.Handle(new CompleteBankLinkCommand(Code, state), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(existingAccount.Id.Value);
        appContext.Accounts.Should().ContainSingle();
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
        var appContext = InMemoryCustomersDbContextFactory.Create();
        var bankLinksContext = InMemoryBankLinksDbContextFactory.Create();
        var customer = await SeedCustomerAsync(appContext);
        var cache = new FakeDistributedCache();
        var state = await SeedPendingStateAsync(cache, bankLinksContext, customer.Id, Institution.FNB);
        var handler = BuildHandler(
            bankLinksContext,
            appContext,
            BuildClient(linkedAccount: new AggregatorLinkedAccountResult("ext-1", "ACC-001", "Account", aggregatorType, "ZAR")),
            cache);

        await handler.Handle(new CompleteBankLinkCommand(Code, state), CancellationToken.None);

        appContext.Accounts.Single().AccountType.Should().Be(expected);
    }
}