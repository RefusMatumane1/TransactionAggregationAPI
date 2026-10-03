using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Modules.Audit.Application.Contracts;
using Modules.Audit.Contracts;
using Npgsql;
using Xunit;

namespace TransactionAggregation.Tests.Integration.Postgres
{
    [Collection(PostgresCollection.Name)]
    public class AuditTrailPostgresTests
    {
        private readonly PostgresContainerFixture _fixture;

        public AuditTrailPostgresTests(PostgresContainerFixture fixture)
        {
            _fixture = fixture;
        }

        private static AuditEventRecord Record(Guid? id = null) => new(
            EventId: id ?? Guid.NewGuid(),
            EventType: AuditEventTypes.InboundReceived,
            OccurredAt: DateTime.UtcNow,
            Channel: AuditChannels.Kafka,
            SourceName: $"pg-audit-{Guid.NewGuid():N}",
            Metadata: new Dictionary<string, string> { ["topic"] = "bank-transactions", ["offset"] = "42" });

        private async Task<Guid> SeedAsync()
        {
            var record = Record();
            using var context = _fixture.CreateAuditContext();
            await new AuditTrail(context, NullLogger<AuditTrail>.Instance).RecordAsync([record]);
            return record.EventId;
        }

        [Fact]
        public async Task RecordAsync_MetadataRoundTripsThroughJsonb()
        {
            var id = await SeedAsync();

            using var context = _fixture.CreateAuditContext();
            var stored = await context.AuditEvents.SingleAsync(e => e.Id == id);

            stored.Metadata.Should().Contain("topic", "bank-transactions").And.Contain("offset", "42");
        }

        [Theory]
        [InlineData("""UPDATE audit."AuditEvents" SET "Detail" = 'tampered' WHERE "Id" = @id""")]
        [InlineData("""DELETE FROM audit."AuditEvents" WHERE "Id" = @id""")]
        public async Task AuditEvents_AreAppendOnly_UpdateAndDeleteAreRejected(string sql)
        {
            var id = await SeedAsync();

            await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("id", id);

            var act = () => command.ExecuteNonQueryAsync();

            (await act.Should().ThrowAsync<PostgresException>())
                .Which.MessageText.Should().Contain("append-only");
        }

        [Fact]
        public async Task AuditEvents_AreAppendOnly_TruncateIsRejected()
        {
            await SeedAsync();

            await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand("""TRUNCATE audit."AuditEvents" """, connection);

            var act = () => command.ExecuteNonQueryAsync();

            await act.Should().ThrowAsync<PostgresException>();
        }

        [Fact]
        public async Task RecordAsync_SameEventIdFromConcurrentWriters_IsStoredOnce()
        {
            var record = Record();

            async Task WriteAsync()
            {
                using var context = _fixture.CreateAuditContext();
                await new AuditTrail(context, NullLogger<AuditTrail>.Instance).RecordAsync([record]);
            }

            await Task.WhenAll(WriteAsync(), WriteAsync(), WriteAsync());

            using var verify = _fixture.CreateAuditContext();
            (await verify.AuditEvents.CountAsync(e => e.Id == record.EventId)).Should().Be(1);
        }
    }
}