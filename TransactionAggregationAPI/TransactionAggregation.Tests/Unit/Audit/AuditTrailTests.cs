using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Modules.Audit.Application.Contracts;
using Modules.Audit.Contracts;
using Modules.Audit.Domain;
using SharedKernel.Exceptions;
using TransactionAggregation.Tests.Helpers;
using Xunit;

namespace TransactionAggregation.Tests.Unit.Audit;

public class AuditTrailTests
{
    private static AuditEventRecord Record(Guid? id = null, string type = AuditEventTypes.InboundReceived) => new(
        EventId: id ?? Guid.NewGuid(),
        EventType: type,
        OccurredAt: new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc),
        Channel: AuditChannels.Webhook,
        SourceName: "source-1",
        ExternalAccountId: "ext-acc-1",
        InboxMessageId: Guid.NewGuid(),
        IdempotencyKey: "delivery-1",
        Detail: "1 transaction(s) queued",
        Metadata: new Dictionary<string, string> { ["remoteIp"] = "10.0.0.1" },
        TraceId: "trace-1");

    [Fact]
    public async Task RecordAsync_StoresEveryFieldOfTheEvent()
    {
        var context = InMemoryAuditDbContextFactory.Create();
        var sut = new AuditTrail(context, NullLogger<AuditTrail>.Instance);
        var record = Record();

        await sut.RecordAsync([record]);

        var stored = context.AuditEvents.Should().ContainSingle().Subject;
        stored.Id.Should().Be(record.EventId);
        stored.EventType.Should().Be(record.EventType);
        stored.OccurredAt.Should().Be(record.OccurredAt);
        stored.Channel.Should().Be(AuditChannels.Webhook);
        stored.SourceName.Should().Be("source-1");
        stored.ExternalAccountId.Should().Be("ext-acc-1");
        stored.InboxMessageId.Should().Be(record.InboxMessageId);
        stored.IdempotencyKey.Should().Be("delivery-1");
        stored.Detail.Should().Be(record.Detail);
        stored.Metadata.Should().ContainKey("remoteIp").WhoseValue.Should().Be("10.0.0.1");
        stored.TraceId.Should().Be("trace-1");
        stored.RecordedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task RecordAsync_SameEventIdRecordedTwice_IsStoredOnce()
    {
        var context = InMemoryAuditDbContextFactory.Create();
        var sut = new AuditTrail(context, NullLogger<AuditTrail>.Instance);
        var record = Record();

        await sut.RecordAsync([record]);
        await sut.RecordAsync([record, Record()]);

        context.AuditEvents.Should().HaveCount(2);
        context.AuditEvents.Count(e => e.Id == record.EventId).Should().Be(1);
    }

    [Fact]
    public async Task RecordAsync_DuplicateIdsWithinOneBatch_AreStoredOnce()
    {
        var context = InMemoryAuditDbContextFactory.Create();
        var sut = new AuditTrail(context, NullLogger<AuditTrail>.Instance);
        var id = Guid.NewGuid();

        await sut.RecordAsync([Record(id), Record(id)]);

        context.AuditEvents.Should().ContainSingle();
    }

    [Fact]
    public async Task RecordAsync_EmptyBatch_IsANoOp()
    {
        var context = InMemoryAuditDbContextFactory.Create();
        var sut = new AuditTrail(context, NullLogger<AuditTrail>.Instance);

        await sut.RecordAsync([]);

        context.AuditEvents.Should().BeEmpty();
    }

    [Theory]
    [InlineData("", "webhook", "source")]
    [InlineData("inbound.received", "", "source")]
    [InlineData("inbound.received", "webhook", "")]
    public void Create_MissingRequiredField_Throws(string eventType, string channel, string source)
    {
        var act = () => AuditEvent.Create(Guid.NewGuid(), eventType, DateTime.UtcNow, channel, source);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Create_OverlongDetail_IsTruncatedRatherThanRejected()
    {
        var detail = new string('x', AuditEvent.MaxDetailLength + 500);

        var audit = AuditEvent.Create(Guid.NewGuid(), "inbound.rejected", DateTime.UtcNow, "kafka", "source", detail: detail);

        audit.Detail!.Length.Should().Be(AuditEvent.MaxDetailLength);
    }

    [Fact]
    public void Deterministic_SameNaturalKey_GivesSameId_DifferentKey_DifferentId()
    {
        var a = AuditEventIds.Deterministic("kafka:t:0:1:rejected");

        a.Should().Be(AuditEventIds.Deterministic("kafka:t:0:1:rejected"));
        a.Should().NotBe(AuditEventIds.Deterministic("kafka:t:0:2:rejected"));
    }
}