# ADR-0002: PostgreSQL as the system of record

## Status
Accepted

## Context
Financial transaction data requires strong consistency guarantees: unique
constraints for idempotency, foreign-key integrity between customers/accounts/
transactions, transactional writes across the transaction + outbox tables, and
decimal-accurate monetary arithmetic. It does not require multi-region
active-active writes or a flexible/schema-less document model — transactions
have a well-defined, stable shape per the domain model.

## Decision
Use PostgreSQL as the single relational system of record, accessed through EF
Core (`TransactionAggregation.Persistence`). Money is stored as `decimal` with an
explicit currency column (`Money` value object), never `float`/`double`.

Concretely:
- Composite/targeted indexes support the actual query patterns (e.g.
  `CustomerId+Date+Category`, `CustomerId+Status` on the transactions table —
  see `TransactionConfiguration.cs`), not a blanket "index everything" policy.
- Idempotency at the ingestion boundary is enforced by a **database-level unique
  constraint** on `(CustomerId, Source.ExternalId)` combined with catching the
  resulting `DbUpdateException` (Postgres error 23505) in
  `ProcessInboundTransactionsCommandHandler`, not by a check-then-insert
  application-level guard, which is a known race condition under concurrent
  delivery of the same event.

## Consequences
- Strong consistency and referential integrity come from the database, not from
  application discipline — the database rejects duplicate transactions even if
  two concurrent requests both pass an application-level "does this exist?"
  check before either commits.
- We give up the schema flexibility a document store would offer; this is
  acceptable because provider payloads are normalized into a stable canonical
  `Transaction` shape before persistence — providers are never allowed to leak
  their own schema into storage.
- No read/write splitting or multi-region replication exists yet. Should read
  load or geographic latency become a real constraint, PostgreSQL read replicas
  are a well-understood next step; nothing here forecloses it.

## Alternatives considered
- **A document database (e.g. MongoDB)** — rejected: no schema-flexibility need
  exists per-provider (providers are normalized before storage), and document
  stores make the uniqueness/foreign-key guarantees this ADR relies on harder to
  get right at the database layer.
- **Redis or another in-memory store as primary storage** — rejected explicitly;
  see ADR-0005. Redis is not durable enough to be a system of record for
  financial data.
