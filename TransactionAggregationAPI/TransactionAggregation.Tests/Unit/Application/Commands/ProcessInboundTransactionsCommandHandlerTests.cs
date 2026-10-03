using BuildingBlocks.Messaging.Persistence;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Modules.Transactions.Application.Common.DTOs;
using Modules.Transactions.Application.Common.Interfaces;
using Modules.Transactions.Application.Common.Outbox;
using Modules.Transactions.Application.Features.Transactions.Commands.ProcessInboundTransactions;
using Modules.Transactions.Contracts.IntegrationEvents;
using Modules.Transactions.Domain.Enums;
using Modules.Transactions.Infrastructure.Persistence;
using NSubstitute;
using SharedKernel.Common.Enums;
using System.Text.Json;
using TransactionAggregation.Tests.Helpers;
using Xunit;

namespace TransactionAggregation.Tests.Unit.Application.Commands
{
    public class ProcessInboundTransactionsCommandHandlerTests
    {
        private const string Source = TestInstitutions.FNB;
        private const string Account = "ext-acc-1";

        private static ProcessInboundTransactionsCommandHandler BuildHandler(
            TransactionsDbContext ctx,
            IMessagingDbContext messaging,
            ITransactionCategorizationService? categorizationService = null)
            => new(
                ctx,
                messaging,
                TestNormalizers.Neutral,
                categorizationService ?? BuildCategorizationService(),
                NullLogger<ProcessInboundTransactionsCommandHandler>.Instance);

        private static ITransactionCategorizationService BuildCategorizationService(TransactionCategory category = TransactionCategory.Uncategorized)
        {
            var service = Substitute.For<ITransactionCategorizationService>();
            service.Categorize(Arg.Any<string>(), Arg.Any<decimal>(), Arg.Any<TransactionCategory?>())
                .Returns(category);
            return service;
        }

        private static ProcessInboundTransactionsCommand Command(
            IReadOnlyList<ExternalTransactionDTO> transactions,
            string account = Account,
            string? institution = null,
            Guid? inboxMessageId = null,
            string source = Source) =>
            new(source, account, institution, transactions, inboxMessageId);

        private static ExternalTransactionDTO MakeDto(string id = "txn-1", decimal amount = -150m) => new()
        {
            Id = id,
            Amount = amount,
            Currency = "ZAR",
            Description = "Woolworths",
            Category = string.Empty,
            Date = DateTime.UtcNow
        };

        private static (TransactionsDbContext Context, MessagingDbContext Messaging) NewContexts()
        {
            var messaging = InMemoryMessagingDbContextFactory.Create();
            return (InMemoryDbContextFactory.Create(messagingDbContext: messaging), messaging);
        }

        [Fact]
        public async Task Handle_NewDelivery_StoresTheSourceBankAndAccountOnEachTransaction()
        {
            var (context, messaging) = NewContexts();
            var handler = BuildHandler(context, messaging);

            var result = await handler.Handle(Command([MakeDto()]), CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            result.Value.Should().Be(1);
            var stored = context.Transactions.Single();
            stored.Source.Name.Should().Be(TestInstitutions.FNB);
            stored.ExternalAccountId.Should().Be(Account);
        }

        [Fact]
        public async Task Handle_MultipleTransactionsInOneBatch_PersistsAll()
        {
            var (context, messaging) = NewContexts();
            var handler = BuildHandler(context, messaging);

            var result = await handler.Handle(Command([MakeDto("txn-1"), MakeDto("txn-2"), MakeDto("txn-3")]), CancellationToken.None);

            result.Value.Should().Be(3);
            context.Transactions.Should().HaveCount(3);
        }

        [Fact]
        public async Task Handle_CategorizationServiceReturnsCategory_AppliesItToTheTransaction()
        {
            var (context, messaging) = NewContexts();
            var handler = BuildHandler(context, messaging, BuildCategorizationService(TransactionCategory.Groceries));

            await handler.Handle(Command([MakeDto()]), CancellationToken.None);

            context.Transactions.Single().Category.Should().Be(TransactionCategory.Groceries);
        }

        [Fact]
        public async Task Handle_Category_IsDecidedAtCreation_NotChangedAfterwards()
        {
            var (context, messaging) = NewContexts();
            var handler = BuildHandler(context, messaging, BuildCategorizationService(TransactionCategory.Groceries));

            await handler.Handle(Command([MakeDto()]), CancellationToken.None);

            context.Transactions.Single().Category.Should().Be(TransactionCategory.Groceries);
            messaging.OutboxMessages.Select(m => m.Type).Should().OnlyContain(t => t == OutboxMessageTypes.TransactionRecorded,
                "a new transaction is announced once; there is no separate re-categorisation");
        }

        [Fact]
        public async Task Handle_NewTransaction_EnqueuesTransactionRecordedOutboxMessage()
        {
            var (context, messaging) = NewContexts();
            var handler = BuildHandler(context, messaging);

            await handler.Handle(Command([MakeDto()]), CancellationToken.None);

            var transaction = context.Transactions.Single();
            var message = messaging.OutboxMessages.Should().ContainSingle(
                m => m.Type == OutboxMessageTypes.TransactionRecorded).Subject;
            JsonSerializer.Deserialize<TransactionRecorded>(message.Payload)!
                .TransactionId.Should().Be(transaction.Id.Value);
        }

        [Fact]
        public async Task Handle_DeliveryNamingAnotherBank_IsRefusedAndStoresNothing()
        {
            var (context, messaging) = NewContexts();
            var handler = BuildHandler(context, messaging);

            var result = await handler.Handle(Command([MakeDto()], institution: TestInstitutions.Absa), CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            result.Error.Type.Should().Be(ErrorType.Forbidden);
            context.Transactions.Should().BeEmpty();
        }

        [Fact]
        public async Task Handle_DeliveryWithoutAnInstitution_IsStoredUnderTheSourceBank()
        {
            var (context, messaging) = NewContexts();
            var handler = BuildHandler(context, messaging);

            var result = await handler.Handle(Command([MakeDto()], institution: null, source: TestInstitutions.Capitec), CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            context.Transactions.Single().Source.Name.Should().Be(TestInstitutions.Capitec);
        }

        [Fact]
        public async Task Handle_DeliveryNamingItsOwnBankInAnotherCase_IsAccepted()
        {
            var (context, messaging) = NewContexts();
            var handler = BuildHandler(context, messaging);

            var result = await handler.Handle(Command([MakeDto()], institution: TestInstitutions.FNB.ToLowerInvariant()), CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            context.Transactions.Single().Source.Name.Should().Be(TestInstitutions.FNB);
        }

        [Fact]
        public async Task Handle_SameBankTransactionIdOnAnotherAccountOrBank_IsADifferentTransaction()
        {
            var (context, messaging) = NewContexts();
            var handler = BuildHandler(context, messaging);

            await handler.Handle(Command([MakeDto("txn-1")]), CancellationToken.None);
            await handler.Handle(Command([MakeDto("txn-1")], account: "ext-acc-2"), CancellationToken.None);
            await handler.Handle(Command([MakeDto("txn-1")], source: TestInstitutions.Absa), CancellationToken.None);

            context.Transactions.Should().HaveCount(3);
        }

        [Fact]
        public async Task Handle_RedeliveredTransaction_IsSkippedNotDuplicated()
        {
            var (context, messaging) = NewContexts();
            var handler = BuildHandler(context, messaging);

            var first = await handler.Handle(Command([MakeDto("txn-1")]), CancellationToken.None);
            var redelivered = await handler.Handle(Command([MakeDto("txn-1")]), CancellationToken.None);

            first.Value.Should().Be(1);
            redelivered.IsSuccess.Should().BeTrue();
            redelivered.Value.Should().Be(0);
            context.Transactions.Should().ContainSingle();
        }

        [Fact]
        public async Task Handle_BatchMixingNewAndKnownTransactions_OnlyPersistsTheNewOnes()
        {
            var (context, messaging) = NewContexts();
            var handler = BuildHandler(context, messaging);

            await handler.Handle(Command([MakeDto("txn-1")]), CancellationToken.None);
            var result = await handler.Handle(Command([MakeDto("txn-1"), MakeDto("txn-2")]), CancellationToken.None);

            result.Value.Should().Be(1);
            context.Transactions.Should().HaveCount(2);
        }

        [Fact]
        public async Task Handle_SameExternalIdTwiceInOneBatch_PersistsOnceAndStillPersistsTheRest()
        {
            var (context, messaging) = NewContexts();
            var handler = BuildHandler(context, messaging);

            var result = await handler.Handle(Command([MakeDto("txn-1"), MakeDto("txn-1"), MakeDto("txn-2")]), CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            result.Value.Should().Be(2);
            context.Transactions.Select(t => t.Source.ExternalId).Should().BeEquivalentTo(["txn-1", "txn-2"]);
        }

        [Fact]
        public async Task Handle_BatchWithDuplicates_EnqueuesTransactionLevelDuplicateNotification()
        {
            var (context, messaging) = NewContexts();
            var handler = BuildHandler(context, messaging);
            var inboxMessageId = Guid.NewGuid();

            await handler.Handle(Command([MakeDto("txn-1")]), CancellationToken.None);
            await handler.Handle(Command([MakeDto("txn-1"), MakeDto("txn-2"), MakeDto("txn-2")], inboxMessageId: inboxMessageId), CancellationToken.None);

            var message = messaging.OutboxMessages.Should()
                .ContainSingle(m => m.Type == OutboxMessageTypes.DuplicateInboundDetected).Subject;
            var payload = JsonSerializer.Deserialize<DuplicateInboundDetectedOutboxPayload>(message.Payload)!;
            payload.Level.Should().Be("transaction");
            payload.DuplicateExternalIds.Should().BeEquivalentTo(["txn-1", "txn-2"]);
            payload.InboxMessageId.Should().Be(inboxMessageId);
            payload.ExternalAccountId.Should().Be(Account);
        }

        [Fact]
        public async Task Handle_EntireBatchAlreadyStored_ReturnsZeroAndStillNotifies()
        {
            var (context, messaging) = NewContexts();
            var handler = BuildHandler(context, messaging);
            var command = Command([MakeDto("txn-1")]);

            await handler.Handle(command, CancellationToken.None);
            var redelivered = await handler.Handle(command, CancellationToken.None);

            redelivered.IsSuccess.Should().BeTrue();
            redelivered.Value.Should().Be(0);
            messaging.OutboxMessages.Should().ContainSingle(m => m.Type == OutboxMessageTypes.DuplicateInboundDetected);
        }

        [Fact]
        public async Task Handle_NoDuplicates_EnqueuesNoDuplicateNotification()
        {
            var (context, messaging) = NewContexts();
            var handler = BuildHandler(context, messaging);

            await handler.Handle(Command([MakeDto("txn-1"), MakeDto("txn-2")]), CancellationToken.None);

            messaging.OutboxMessages.Should().NotContain(m => m.Type == OutboxMessageTypes.DuplicateInboundDetected);
        }

        [Fact]
        public async Task Handle_NormalizesEachTransactionAndKeepsWhatTheBankSent()
        {
            var (context, messaging) = NewContexts();
            var categorization = BuildCategorizationService(TransactionCategory.Groceries);
            var normalizer = new Modules.Transactions.Application.Services.TransactionNormalizer(
                Microsoft.Extensions.Options.Options.Create(new Modules.Transactions.Application.Common.Options.NormalizationOptions
                {
                    Default = new() { TimeZone = "Africa/Johannesburg", DescriptionPrefixes = ["POS PURCHASE"], CategoryMap = new() { ["Grocery"] = "Groceries" } }
                }));
            var handler = new ProcessInboundTransactionsCommandHandler(
                context, messaging, normalizer, categorization,
                NullLogger<ProcessInboundTransactionsCommandHandler>.Instance);

            var raw = MakeDto() with
            {
                Description = "POS PURCHASE  Checkers   Sandton",
                Category = "Grocery",
                Date = new DateTime(2026, 9, 10, 12, 0, 0, DateTimeKind.Unspecified)
            };

            await handler.Handle(Command([raw]), CancellationToken.None);

            var stored = context.Transactions.Single();
            stored.Description.Should().Be("Checkers Sandton");
            stored.Date.Should().Be(new DateTime(2026, 9, 10, 10, 0, 0, DateTimeKind.Utc));
            stored.Metadata.Should().Contain("bankDescription", "POS PURCHASE  Checkers   Sandton");
            stored.Metadata.Should().Contain("bankCategory", "Grocery");
            categorization.Received(1).Categorize(
                Arg.Any<string>(), Arg.Any<decimal>(), TransactionCategory.Groceries);
        }
    }
}