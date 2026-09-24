# Architecture Decision Records

| ADR | Decision |
|---|---|
| [0001](0001-modular-monolith-not-microservices.md) | Modular monolith instead of microservices |
| [0002](0002-postgresql-as-system-of-record.md) | PostgreSQL as the system of record |
| [0003](0003-polling-inbox-outbox-not-a-broker.md) | Polling-based Inbox/Outbox instead of a message broker (inbound side superseded by 0012) |
| [0004](0004-no-saga.md) | No Saga |
| [0005](0005-redis-cache-only.md) | Redis is cache-only, never the source of truth |
| [0006](0006-no-partitioning-yet.md) | No database partitioning yet |
| [0007](0007-offset-pagination.md) | Offset pagination for transaction history |
| [0008](0008-single-database-schema.md) | ~~Single PostgreSQL schema, not one schema per module~~ — superseded by 0009 |
| [0009](0009-schema-per-module-database-strategy.md) | Schema-per-module database strategy (one DbContext + one Postgres schema per module) |
| [0010](0010-consumer-owned-ports-for-unextracted-dependencies.md) | Consumer-owned ports for dependencies on not-yet-extracted modules |
| [0011](0011-audit-trail-for-inbound-data.md) | Append-only audit trail for inbound data (Audit module, outbox-transported) |
| [0012](0012-kafka-as-an-additional-inbound-channel.md) | Kafka as an additional inbound channel; internal messaging stays polled (partially supersedes 0003) |
| [0013](0013-webhook-source-institution-scoping.md) | Inbound sources are scoped to the institutions they serve |

Wire formats and their versioning rules: [../event-contracts.md](../event-contracts.md).

See also [../threat-model.md](../threat-model.md) for the STRIDE-based threat model
and [../failure-scenarios.md](../failure-scenarios.md) for detection/response/recovery
per named failure mode.
