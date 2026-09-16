using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using System.Text.Json;
using TransactionAggregation.Application.Common.DTOs;
using TransactionAggregation.Application.Common.Enums;
using TransactionAggregation.Application.Common.Interfaces;
using TransactionAggregation.Application.Common.Outbox;
using TransactionAggregation.Application.Features.Transactions.Commands.ProcessInboundTransactions;
using TransactionAggregation.Domain.Common.ValueObjects;
using TransactionAggregation.Domain.Entities;
using TransactionAggregation.Domain.Enums;
using TransactionAggregation.Domain.Events.Transaction;
using TransactionAggregation.Persistence;
using TransactionAggregation.Tests.Helpers;
using Xunit;

namespace TransactionAggregation.Tests.Unit.Application.Commands;

public class ProcessInboundTransactionsCommandHandlerTests
{
    private static ProcessInboundTransactionsCommandHandler BuildHandler(
        ApplicationDbContext ctx,
        ITransactionCategorizationService? categorizationService = null)
        => new(
            ctx,
            categorizationService ?? BuildCategorizationService(),
            NullLogger<ProcessInboundTransactionsCommandHandler>.Instance);

    private static ITransactionCategorizationService BuildCategorizationService(TransactionCategory category = TransactionCategory.Uncategorized)
    {
        var service = Substitute.For<ITransactionCategorizationService>();
        service.CategorizeTransactionAsync(Arg.Any<Transaction>(), Arg.Any<CancellationToken>())
            .Returns(category);
        return service;
    }

    private static async Task<Customer> SeedCustomerAsync(ApplicationDbContext ctx, string email = "user@example.com")
    {
        var customer = Customer.Create(CustomerId.Create(), email, "Test User");
        ctx.Customers.Add(customer);
        await ctx.SaveChangesAsync();
        return customer;
    }

    private static async Task<BankLink> SeedActiveBankLinkAsync(
        ApplicationDbContext ctx, CustomerId customerId, string externalAccountId = "ext-acc-1")
    {
        var link = BankLink.Create(customerId, Institution.FNB);
        link.Activate(AccountId.Create(), externalAccountId, "enc-access", "enc-refresh", DateTime.UtcNow.AddHours(1));
        ctx.BankLinks.Add(link);
        await ctx.SaveChangesAsync();
        return link;
    }

    private static ExternalTransactionDTO MakeDto(string id = "txn-1", decimal amount = -150m) => new()
    {
        Id = id,
        Amount = amount,
        Currency = "ZAR",
        Description = "Woolworths",
        Category = string.Empty,
        Date = DateTime.UtcNow
    };

    // ── Happy path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ActiveBankLink_PersistsTransactionsForTheLinkedCustomer()
    {
        var context = InMemoryDbContextFactory.Create();
        var customer = await SeedCustomerAsync(context);
        var link = await SeedActiveBankLinkAsync(context, customer.Id);
        var handler = BuildHandler(context);

        var result = await handler.Handle(
            new ProcessInboundTransactionsCommand("test-source", link.ExternalAccountId!, [MakeDto()]), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(1);
        var stored = context.Transactions.Single();
        stored.CustomerId.Should().Be(customer.Id);
        stored.AccountId.Should().Be(link.AccountId);
    }

    [Fact]
    public async Task Handle_MultipleTransactionsInOneBatch_PersistsAll()
    {
        var context = InMemoryDbContextFactory.Create();
        var customer = await SeedCustomerAsync(context);
        var link = await SeedActiveBankLinkAsync(context, customer.Id);
        var handler = BuildHandler(context);

        var result = await handler.Handle(
            new ProcessInboundTransactionsCommand("test-source",
                link.ExternalAccountId!, [MakeDto("txn-1"), MakeDto("txn-2"), MakeDto("txn-3")]),
            CancellationToken.None);

        result.Value.Should().Be(3);
        context.Transactions.Should().HaveCount(3);
    }

    [Fact]
    public async Task Handle_CategorizationServiceReturnsCategory_AppliesItToTheTransaction()
    {
        var context = InMemoryDbContextFactory.Create();
        var customer = await SeedCustomerAsync(context);
        var link = await SeedActiveBankLinkAsync(context, customer.Id);
        var handler = BuildHandler(context, BuildCategorizationService(TransactionCategory.Groceries));

        await handler.Handle(new ProcessInboundTransactionsCommand("test-source", link.ExternalAccountId!, [MakeDto()]), CancellationToken.None);

        context.Transactions.Single().Category.Should().Be(TransactionCategory.Groceries);
    }

    [Fact]
    public async Task Handle_CategorizationServiceReturnsCategory_MarksTheCategorizationAsAutomatic()
    {
        // Uses its own context/mediator (rather than InMemoryDbContextFactory) so the test can
        // observe the TransactionCategorizedDomainEvent raised by Transaction.Categorize —
        // regression test for a bug where the ingestion path called Categorize without isAuto:
        // true, so an ingest-time auto-categorization looked like a manual one downstream.
        var mediator = Substitute.For<IMediator>();
        mediator.Publish(Arg.Any<object>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var context = new ApplicationDbContext(options, mediator);
        var customer = await SeedCustomerAsync(context);
        var link = await SeedActiveBankLinkAsync(context, customer.Id);
        var handler = BuildHandler(context, BuildCategorizationService(TransactionCategory.Groceries));

        await handler.Handle(new ProcessInboundTransactionsCommand("test-source", link.ExternalAccountId!, [MakeDto()]), CancellationToken.None);

        await mediator.Received(1).Publish(
            Arg.Is<TransactionCategorizedDomainEvent>(e => e.IsAutoCategorized),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NewTransaction_EnqueuesTransactionSyncedOutboxMessage()
    {
        var context = InMemoryDbContextFactory.Create();
        var customer = await SeedCustomerAsync(context);
        var link = await SeedActiveBankLinkAsync(context, customer.Id);
        var handler = BuildHandler(context);

        await handler.Handle(new ProcessInboundTransactionsCommand("test-source", link.ExternalAccountId!, [MakeDto()]), CancellationToken.None);

        var transaction = context.Transactions.Single();
        var message = context.OutboxMessages.Should().ContainSingle(
            m => m.Type == OutboxMessageTypes.TransactionSynced).Subject;

        var payload = JsonSerializer.Deserialize<TransactionSyncedOutboxPayload>(message.Payload)!;
        payload.TransactionId.Should().Be(transaction.Id.Value);
        payload.CustomerId.Should().Be(customer.Id.Value);
        payload.SyncSource.Should().Be(link.Institution.ToString());
    }

    // ── Unknown / inactive link ──────────────────────────────────────────────

    [Fact]
    public async Task Handle_UnknownExternalAccountId_ReturnsNotFound()
    {
        var context = InMemoryDbContextFactory.Create();
        var handler = BuildHandler(context);

        var result = await handler.Handle(
            new ProcessInboundTransactionsCommand("test-source", "never-linked", [MakeDto()]), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    [Theory]
    [InlineData(BankLinkStatus.PendingAuthorization)]
    [InlineData(BankLinkStatus.Revoked)]
    [InlineData(BankLinkStatus.NeedsReauthorization)]
    public async Task Handle_LinkNotActive_ReturnsNotFound(BankLinkStatus status)
    {
        var context = InMemoryDbContextFactory.Create();
        var customer = await SeedCustomerAsync(context);
        var link = BankLink.Create(customer.Id, Institution.FNB);

        if (status is BankLinkStatus.Revoked or BankLinkStatus.NeedsReauthorization)
        {
            link.Activate(AccountId.Create(), "ext-acc-1", "enc-a", "enc-r", DateTime.UtcNow.AddHours(1));
            if (status == BankLinkStatus.Revoked)
                link.Revoke();
            else
                link.MarkNeedsReauthorization();
        }
        context.BankLinks.Add(link);
        await context.SaveChangesAsync();

        var handler = BuildHandler(context);

        var result = await handler.Handle(
            new ProcessInboundTransactionsCommand("test-source", "ext-acc-1", [MakeDto()]), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        context.Transactions.Should().BeEmpty();
    }

    // ── Idempotency ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_RedeliveredTransaction_IsSkippedNotDuplicated()
    {
        var context = InMemoryDbContextFactory.Create();
        var customer = await SeedCustomerAsync(context);
        var link = await SeedActiveBankLinkAsync(context, customer.Id);
        var handler = BuildHandler(context);

        var first = await handler.Handle(
            new ProcessInboundTransactionsCommand("test-source", link.ExternalAccountId!, [MakeDto("txn-1")]), CancellationToken.None);
        var redelivered = await handler.Handle(
            new ProcessInboundTransactionsCommand("test-source", link.ExternalAccountId!, [MakeDto("txn-1")]), CancellationToken.None);

        first.Value.Should().Be(1);
        redelivered.IsSuccess.Should().BeTrue();
        redelivered.Value.Should().Be(0);
        context.Transactions.Should().ContainSingle();
    }

    [Fact]
    public async Task Handle_BatchMixingNewAndKnownTransactions_OnlyPersistsTheNewOnes()
    {
        var context = InMemoryDbContextFactory.Create();
        var customer = await SeedCustomerAsync(context);
        var link = await SeedActiveBankLinkAsync(context, customer.Id);
        var handler = BuildHandler(context);

        await handler.Handle(new ProcessInboundTransactionsCommand("test-source", link.ExternalAccountId!, [MakeDto("txn-1")]), CancellationToken.None);

        var result = await handler.Handle(
            new ProcessInboundTransactionsCommand("test-source", link.ExternalAccountId!, [MakeDto("txn-1"), MakeDto("txn-2")]),
            CancellationToken.None);

        result.Value.Should().Be(1);
        context.Transactions.Should().HaveCount(2);
    }
}
