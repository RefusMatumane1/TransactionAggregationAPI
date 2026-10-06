using BuildingBlocks.Messaging.Persistence;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Modules.Audit.Contracts;
using Modules.Transactions.Application.Common.DTOs;
using Modules.Transactions.Application.Common.Interfaces;
using Modules.Transactions.Application.Common.Outbox;
using Modules.Transactions.Application.Features.Transactions.Commands.ProcessInboundTransactions;
using Modules.Transactions.Application.Features.Transactions.Commands.ReceiveBankTransactions;
using Modules.Transactions.Contracts.IntegrationEvents;
using Modules.Transactions.Domain.Enums;
using Modules.Transactions.Infrastructure.Persistence;
using NSubstitute;
using System.Text.Json;
using TransactionAggregation.Tests.Helpers;
using Xunit;

namespace TransactionAggregation.Tests.Unit.Application.Commands
{
    public class LedgerIngestionTests
    {
        private static readonly DateTime PendingDate = new(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc);
        private static readonly DateTime PostedDate = new(2026, 9, 3, 0, 0, 0, DateTimeKind.Utc);

        private sealed record Sut(
            ProcessInboundTransactionsCommandHandler Handler,
            TransactionsDbContext Context,
            MessagingDbContext Messaging);

        private static Sut Build()
        {
            var messaging = InMemoryMessagingDbContextFactory.Create();
            var context = InMemoryDbContextFactory.Create(messagingDbContext: messaging);

            var categorization = Substitute.For<ITransactionCategorizationService>();
            categorization.Categorize(Arg.Any<string>(), Arg.Any<decimal>(), Arg.Any<TransactionCategory?>())
                .Returns(TransactionCategory.Dining);

            var handler = new ProcessInboundTransactionsCommandHandler(
                context, messaging, TestNormalizers.Neutral, categorization,
                NullLogger<ProcessInboundTransactionsCommandHandler>.Instance);
            return new Sut(handler, context, messaging);
        }

        private static ExternalTransactionDTO Dto(
            string id, string? status, decimal amount = -150m, DateTime? date = null, string currency = "ZAR") => new()
            {
                Id = id,
                Amount = amount,
                Currency = currency,
                Description = "Coffee shop",
                Category = string.Empty,
                Date = date ?? PendingDate,
                Status = status
            };

        private static Task<SharedKernel.Common.Models.Result<int>> Deliver(Sut sut, params ExternalTransactionDTO[] dtos) =>
            sut.Handler.Handle(
                new ProcessInboundTransactionsCommand(TestInstitutions.FNB, "ext-acc-1", null, dtos, Guid.NewGuid()),
                CancellationToken.None);

        [Theory]
        [InlineData(null)]
        [InlineData("posted")]
        [InlineData("POSTED")]
        public async Task PostedOrNoStatus_IsRecordedAsALedgerEntry(string? status)
        {
            var sut = Build();

            var result = await Deliver(sut, Dto("txn-1", status));

            result.Value.Should().Be(1);
            sut.Context.Transactions.Single().Status.Should().Be(TransactionStatus.Booked);
        }

        [Fact]
        public async Task Pending_IsAcknowledgedAndAudited_ButNotRecorded()
        {
            var sut = Build();

            var result = await Deliver(sut, Dto("txn-1", "pending"));

            result.Value.Should().Be(0);
            sut.Context.Transactions.Should().BeEmpty("a pending authorisation is not a booked transaction");
            sut.Context.RecordedAudit().Should().ContainSingle(e => e.EventType == AuditEventTypes.TransactionPendingSkipped)
                .Which.ExternalTransactionId.Should().Be("txn-1");
            sut.Context.RecordedAudit().Should().ContainSingle(e => e.EventType == AuditEventTypes.InboundProcessed)
                .Which.Metadata.Should().Contain("pendingCount", "1");
        }

        [Fact]
        public async Task PendingThenPosted_RecordsThePostingWithTheBanksPostedAmountAndDate()
        {
            var sut = Build();
            await Deliver(sut, Dto("txn-1", "pending", amount: -150m));

            await Deliver(sut, Dto("txn-1", "posted", amount: -165m, date: PostedDate));

            var stored = sut.Context.Transactions.Should().ContainSingle().Subject;
            stored.Amount.Amount.Should().Be(-165m);
            stored.Date.Should().Be(PostedDate);
        }

        [Fact]
        public async Task PostedThenStalePending_LeavesTheLedgerUnchanged()
        {
            var sut = Build();
            await Deliver(sut, Dto("txn-1", "posted", amount: -165m));

            await Deliver(sut, Dto("txn-1", "pending", amount: -150m));

            sut.Context.Transactions.Single().Amount.Amount.Should().Be(-165m);
        }

        [Fact]
        public async Task PostedRedelivered_IsSkippedAsADuplicate_AndAlerted()
        {
            var sut = Build();
            await Deliver(sut, Dto("txn-1", "posted"));

            var result = await Deliver(sut, Dto("txn-1", "posted", amount: -999m));

            result.Value.Should().Be(0);
            sut.Context.Transactions.Single().Amount.Amount.Should().Be(-150m, "the first recording stands; a replay never overwrites it");
            sut.Context.RecordedAudit().Should().ContainSingle(e => e.EventType == AuditEventTypes.TransactionDuplicateSkipped)
                .Which.Detail.Should().Be("Already recorded for this account");
            sut.Messaging.OutboxMessages.Should().ContainSingle(m => m.Type == OutboxMessageTypes.DuplicateInboundDetected);
        }

        [Fact]
        public async Task OneDeliveryCarryingPendingAndPosted_RecordsOnlyThePosting()
        {
            var sut = Build();

            await Deliver(sut, Dto("txn-1", "pending", amount: -150m), Dto("txn-1", "posted", amount: -165m));

            sut.Context.Transactions.Should().ContainSingle().Which.Amount.Amount.Should().Be(-165m);
        }

        [Fact]
        public async Task EachRecordedEntry_PublishesASelfContainedTransactionRecordedEvent()
        {
            var sut = Build();

            await Deliver(sut, Dto("txn-1", "posted", amount: -42.5m, date: PostedDate, currency: "USD"));

            var stored = sut.Context.Transactions.Single();
            var message = sut.Messaging.OutboxMessages.Should().ContainSingle(m => m.Type == TransactionRecorded.EventType).Subject;
            message.SchemaVersion.Should().Be(TransactionRecorded.SchemaVersion);
            JsonSerializer.Deserialize<TransactionRecorded>(message.Payload).Should().BeEquivalentTo(new TransactionRecorded(
                EventId: message.Id.Value,
                TransactionId: stored.Id.Value,
                Institution: TestInstitutions.FNB,
                ExternalAccountId: "ext-acc-1",
                ExternalTransactionId: "txn-1",
                Amount: -42.5m,
                Currency: "USD",
                Description: "Coffee shop",
                Category: nameof(TransactionCategory.Dining),
                BookedAt: PostedDate,
                RecordedAt: stored.CreatedAt));
        }

        [Theory]
        [InlineData("pending", true)]
        [InlineData("posted", true)]
        [InlineData(null, true)]
        [InlineData("booked", false)]
        [InlineData("", false)]
        public void Validator_AcceptsOnlyPendingPostedOrNoStatus(string? status, bool valid)
        {
            var command = new ReceiveBankTransactionsCommand("source-1", "ext-acc-1", null, [Dto("txn-1", status)]);

            new ReceiveBankTransactionsCommandValidator().Validate(command).IsValid.Should().Be(valid);
        }
    }
}