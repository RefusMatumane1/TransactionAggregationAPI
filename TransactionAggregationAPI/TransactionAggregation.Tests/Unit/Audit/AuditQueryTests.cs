using FluentAssertions;
using Modules.Audit.Application.Features.GetTransactionLineage;
using Modules.Audit.Application.Features.SearchAuditEvents;
using Modules.Audit.Contracts;
using Modules.Audit.Domain;
using Modules.Audit.Infrastructure.Persistence;
using SharedKernel.Common.Enums;
using TransactionAggregation.Tests.Helpers;
using Xunit;

namespace TransactionAggregation.Tests.Unit.Audit
{
    public class AuditQueryTests
    {
        // Written by the lifecycle that preceded the insert-only ledger; lineage still shows such history.
        private const string HistoricalSettledEventType = "transaction.settled";

        private static readonly DateTime T0 = new(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc);

        private static AuditEvent Event(
            string type, DateTime at, string channel = AuditChannels.Webhook, string source = "source-1",
            Guid? inboxMessageId = null, Guid? transactionId = null, string? externalTransactionId = null,
            string? externalAccountId = "ext-acc-1") =>
            AuditEvent.Create(Guid.NewGuid(), type, at, channel, source, externalAccountId, inboxMessageId,
                transactionId: transactionId, externalTransactionId: externalTransactionId,
                metadata: new Dictionary<string, string> { ["k"] = "v" });

        private static async Task<AuditDbContext> SeedAsync(params AuditEvent[] events)
        {
            var context = InMemoryAuditDbContextFactory.Create();
            context.AuditEvents.AddRange(events);
            await context.SaveChangesAsync();
            return context;
        }

        [Fact]
        public async Task Search_NoFilters_ReturnsNewestFirstWithTotalCount()
        {
            var context = await SeedAsync(
                Event(AuditEventTypes.InboundReceived, T0),
                Event(AuditEventTypes.InboundReceived, T0.AddMinutes(2)),
                Event(AuditEventTypes.InboundReceived, T0.AddMinutes(1)));
            var sut = new SearchAuditEventsQueryHandler(context, new InMemoryKeysetPaginator());

            var result = await sut.Handle(new SearchAuditEventsQuery { IncludeTotal = true }, CancellationToken.None);

            result.Value.TotalCount.Should().Be(3);
            result.Value.Items.Select(e => e.OccurredAt).Should().BeInDescendingOrder();
        }

        [Fact]
        public async Task Search_ByChannelAndEventType_ReturnsOnlyMatches()
        {
            var context = await SeedAsync(
                Event(AuditEventTypes.InboundReceived, T0, AuditChannels.Kafka),
                Event(AuditEventTypes.InboundRejected, T0, AuditChannels.Kafka),
                Event(AuditEventTypes.InboundReceived, T0, AuditChannels.Webhook));
            var sut = new SearchAuditEventsQueryHandler(context, new InMemoryKeysetPaginator());

            var result = await sut.Handle(
                new SearchAuditEventsQuery { Channel = AuditChannels.Kafka, EventType = AuditEventTypes.InboundReceived },
                CancellationToken.None);

            result.Value.Items.Should().ContainSingle()
                .Which.Should().Match<Modules.Audit.Application.DTOs.AuditEventDto>(e =>
                    e.Channel == AuditChannels.Kafka && e.EventType == AuditEventTypes.InboundReceived);
        }

        [Fact]
        public async Task Search_ByTimeRangeAndSource_ReturnsOnlyEventsInside()
        {
            var context = await SeedAsync(
                Event(AuditEventTypes.InboundReceived, T0.AddHours(-1)),
                Event(AuditEventTypes.InboundReceived, T0),
                Event(AuditEventTypes.InboundReceived, T0, source: "other-source"),
                Event(AuditEventTypes.InboundReceived, T0.AddHours(1)));
            var sut = new SearchAuditEventsQueryHandler(context, new InMemoryKeysetPaginator());

            var result = await sut.Handle(
                new SearchAuditEventsQuery { SourceName = "source-1", From = T0.AddMinutes(-5), To = T0.AddMinutes(5) },
                CancellationToken.None);

            result.Value.Items.Should().ContainSingle().Which.OccurredAt.Should().Be(T0);
        }

        [Fact]
        public async Task Search_Paging_ReturnsTheRequestedSlice()
        {
            var events = Enumerable.Range(0, 5).Select(i => Event(AuditEventTypes.InboundReceived, T0.AddMinutes(i))).ToArray();
            var context = await SeedAsync(events);
            var sut = new SearchAuditEventsQueryHandler(context, new InMemoryKeysetPaginator());

            var first = await sut.Handle(new SearchAuditEventsQuery { PageSize = 2 }, CancellationToken.None);
            var result = await sut.Handle(new SearchAuditEventsQuery { PageSize = 2, Cursor = first.Value.NextCursor }, CancellationToken.None);

            first.Value.Items.Select(e => e.OccurredAt).Should().Equal(T0.AddMinutes(4), T0.AddMinutes(3));
            result.Value.Items.Select(e => e.OccurredAt).Should().Equal(T0.AddMinutes(2), T0.AddMinutes(1));
            result.Value.HasMore.Should().BeTrue();
        }

        [Theory]
        [InlineData(0, null)]
        [InlineData(201, null)]
        [InlineData(50, "not-a-cursor")]
        public void SearchValidator_RejectsOutOfRangePagingOrAnInvalidCursor(int pageSize, string? cursor)
        {
            var result = new SearchAuditEventsQueryValidator().Validate(
                new SearchAuditEventsQuery { PageSize = pageSize, Cursor = cursor });

            result.IsValid.Should().BeFalse();
        }

        [Fact]
        public void SearchValidator_RejectsFromAfterTo()
        {
            var result = new SearchAuditEventsQueryValidator().Validate(
                new SearchAuditEventsQuery { From = T0, To = T0.AddDays(-1) });

            result.IsValid.Should().BeFalse();
        }

        [Fact]
        public async Task Lineage_ReturnsChannelSourceAndTheDeliveryEventsOfTheTransaction()
        {
            var inboxId = Guid.NewGuid();
            var transactionId = Guid.NewGuid();
            var otherTransactionInSameBatch = Guid.NewGuid();
            var context = await SeedAsync(
                Event(AuditEventTypes.InboundReceived, T0, AuditChannels.Kafka, inboxMessageId: inboxId),
                Event(AuditEventTypes.InboundProcessingFailed, T0.AddSeconds(5), AuditChannels.Kafka, inboxMessageId: inboxId),
                Event(AuditEventTypes.TransactionIngested, T0.AddSeconds(20), AuditChannels.Kafka, inboxMessageId: inboxId,
                    transactionId: transactionId, externalTransactionId: "txn-1"),
                Event(AuditEventTypes.TransactionIngested, T0.AddSeconds(20), AuditChannels.Kafka, inboxMessageId: inboxId,
                    transactionId: otherTransactionInSameBatch, externalTransactionId: "txn-2"),
                Event(AuditEventTypes.InboundProcessed, T0.AddSeconds(20), AuditChannels.Kafka, inboxMessageId: inboxId),
                Event(AuditEventTypes.InboundReceived, T0, inboxMessageId: Guid.NewGuid()));
            var sut = new GetTransactionLineageQueryHandler(context);

            var result = await sut.Handle(new GetTransactionLineageQuery(transactionId), CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            var lineage = result.Value;
            lineage.Channel.Should().Be(AuditChannels.Kafka);
            lineage.SourceName.Should().Be("source-1");
            lineage.ExternalTransactionId.Should().Be("txn-1");
            lineage.InboxMessageId.Should().Be(inboxId);
            lineage.ReceivedAt.Should().Be(T0);
            lineage.IngestedAt.Should().Be(T0.AddSeconds(20));
            lineage.Events.Select(e => e.EventType).Should().Equal(
                AuditEventTypes.InboundReceived,
                AuditEventTypes.InboundProcessingFailed,
                AuditEventTypes.TransactionIngested,
                AuditEventTypes.InboundProcessed);
            lineage.Events.Should().NotContain(e => e.TransactionId == otherTransactionInSameBatch);
        }

        [Fact]
        public async Task Lineage_IncludesTheSettlementThatArrivedInALaterDelivery()
        {
            var pendingDelivery = Guid.NewGuid();
            var postedDelivery = Guid.NewGuid();
            var transactionId = Guid.NewGuid();
            var context = await SeedAsync(
                Event(AuditEventTypes.InboundReceived, T0, inboxMessageId: pendingDelivery),
                Event(AuditEventTypes.TransactionIngested, T0.AddSeconds(5), inboxMessageId: pendingDelivery,
                    transactionId: transactionId, externalTransactionId: "txn-1"),
                Event(AuditEventTypes.InboundReceived, T0.AddDays(2), inboxMessageId: postedDelivery),
                Event(HistoricalSettledEventType, T0.AddDays(2).AddSeconds(5), inboxMessageId: postedDelivery,
                    transactionId: transactionId, externalTransactionId: "txn-1"));
            var sut = new GetTransactionLineageQueryHandler(context);

            var lineage = (await sut.Handle(new GetTransactionLineageQuery(transactionId), CancellationToken.None)).Value;

            lineage.InboxMessageId.Should().Be(pendingDelivery, "the origin is the delivery that first brought the transaction in");
            lineage.Events.Select(e => e.EventType).Should().Equal(
                AuditEventTypes.InboundReceived,
                AuditEventTypes.TransactionIngested,
                HistoricalSettledEventType);
        }

        [Fact]
        public async Task Lineage_TransactionWithoutIngestionRecord_ReturnsNotFound()
        {
            var context = await SeedAsync(Event(AuditEventTypes.InboundReceived, T0));
            var sut = new GetTransactionLineageQueryHandler(context);

            var result = await sut.Handle(new GetTransactionLineageQuery(Guid.NewGuid()), CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            result.Error.Type.Should().Be(ErrorType.NotFound);
        }
    }
}