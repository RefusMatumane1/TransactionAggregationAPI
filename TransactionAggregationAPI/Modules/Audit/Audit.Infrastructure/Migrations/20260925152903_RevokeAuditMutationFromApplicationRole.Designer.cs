using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Modules.Audit.Infrastructure.Persistence;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Modules.Audit.Infrastructure.Migrations
{
    [DbContext(typeof(AuditDbContext))]
    [Migration("20260925152903_RevokeAuditMutationFromApplicationRole")]
    partial class RevokeAuditMutationFromApplicationRole
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasDefaultSchema("audit")
                .HasAnnotation("ProductVersion", "10.0.5")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("Modules.Audit.Domain.AuditEvent", b =>
                {
                    b.Property<Guid>("Id")
                        .HasColumnType("uuid");

                    b.Property<string>("Channel")
                        .IsRequired()
                        .HasMaxLength(32)
                        .HasColumnType("character varying(32)");

                    b.Property<Guid?>("CustomerId")
                        .HasColumnType("uuid");

                    b.Property<string>("Detail")
                        .HasMaxLength(2000)
                        .HasColumnType("character varying(2000)");

                    b.Property<string>("EventType")
                        .IsRequired()
                        .HasMaxLength(64)
                        .HasColumnType("character varying(64)");

                    b.Property<string>("ExternalAccountId")
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)");

                    b.Property<string>("ExternalTransactionId")
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)");

                    b.Property<string>("IdempotencyKey")
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)");

                    b.Property<Guid?>("InboxMessageId")
                        .HasColumnType("uuid");

                    b.Property<string>("Metadata")
                        .IsRequired()
                        .HasColumnType("jsonb");

                    b.Property<DateTime>("OccurredAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<DateTime>("RecordedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("SourceName")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)");

                    b.Property<string>("TraceId")
                        .HasMaxLength(64)
                        .HasColumnType("character varying(64)");

                    b.Property<Guid?>("TransactionId")
                        .HasColumnType("uuid");

                    b.HasKey("Id");

                    b.HasIndex("InboxMessageId")
                        .HasDatabaseName("IX_AuditEvents_InboxMessageId");

                    b.HasIndex("OccurredAt")
                        .HasDatabaseName("IX_AuditEvents_OccurredAt");

                    b.HasIndex("TransactionId")
                        .HasDatabaseName("IX_AuditEvents_TransactionId");

                    b.HasIndex("EventType", "OccurredAt")
                        .HasDatabaseName("IX_AuditEvents_EventType_OccurredAt");

                    b.HasIndex("ExternalAccountId", "OccurredAt")
                        .HasDatabaseName("IX_AuditEvents_ExternalAccountId_OccurredAt");

                    b.HasIndex("Channel", "SourceName", "OccurredAt")
                        .HasDatabaseName("IX_AuditEvents_Channel_Source_OccurredAt");

                    b.ToTable("AuditEvents", "audit");
                });
#pragma warning restore 612, 618
        }
    }
}
