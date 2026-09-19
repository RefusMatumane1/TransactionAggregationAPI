# Architecture Decision Records

| ADR | Decision |
|---|---|
| [0001](0001-modular-monolith-not-microservices.md) | Modular monolith instead of microservices |
| [0002](0002-postgresql-as-system-of-record.md) | PostgreSQL as the system of record |
| [0003](0003-polling-inbox-outbox-not-a-broker.md) | Polling-based Inbox/Outbox instead of a message broker |
| [0004](0004-no-saga.md) | No Saga |
| [0005](0005-redis-cache-only.md) | Redis is cache-only, never the source of truth |
| [0006](0006-no-partitioning-yet.md) | No database partitioning yet |
| [0007](0007-offset-pagination.md) | Offset pagination for transaction history |
| [0008](0008-single-database-schema.md) | ~~Single PostgreSQL schema, not one schema per module~~ — superseded by 0009 |
| [0009](0009-schema-per-module-database-strategy.md) | Schema-per-module database strategy (one DbContext + one Postgres schema per module) |

See also [../threat-model.md](../threat-model.md) for the STRIDE-based threat model
and [../failure-scenarios.md](../failure-scenarios.md) for detection/response/recovery
per named failure mode.
