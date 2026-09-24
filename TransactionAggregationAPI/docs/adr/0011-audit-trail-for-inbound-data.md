# ADR-0011: Audit trail for inbound data

## Status
Accepted

## Context
Transactions now arrive through more than one channel (the REST webhook, and Kafka),
and each delivery can be accepted, recognised as a replay, requeued, rejected,
retried, dead-lettered, or partly skipped because some transactions were already
stored. Logs (Seq) show some of this, but logs get sampled, rotated, and aren't
something you can query per transaction months later. We need to be able to answer,
for any stored transaction or refused delivery:

- which channel it came through and from which source;
- when it was received and when it was processed;
- the channel's own facts (remote IP / user agent / request id for webhooks,
  topic / partition / offset for Kafka), the idempotency key, and a payload hash;
- what happened to the delivery afterwards (retries, dead-letter, duplicates skipped).

## Decision
Add an **Audit module** (`Modules/Audit/{Contracts,Domain,Application,Infrastructure}`)
that owns an append-only `audit."AuditEvents"` table (own schema and migrations, per
ADR-0009), with one row per fact.

**Other modules depend only on `Audit.Contracts`.** It publishes `IAuditTrail`,
`AuditEventRecord`, the channel/event-type names, and the outbox message type. The
Audit module depends on no other module. `AuditModuleIsolationTests` enforces both
rules.

**Two write paths, chosen by whether the fact changes the database:**

1. **Facts that describe a database change** (delivery received / duplicate /
   requeued, transaction ingested / duplicate skipped, delivery processed / failed /
   dead-lettered) are queued as an `AuditEvents` outbox message **in the same
   SaveChanges as the change itself**. The existing outbox dispatcher hands them to
   `IAuditTrail`. So a fact is audited if and only if it was committed: no audit
   record for a rolled-back insert, and no committed insert missing its record.
2. **Facts with no database change** (unauthorized webhook call, validation failure,
   malformed Kafka record) are recorded directly through `IAuditTrail`.
   - Webhook: best effort. The caller already gets a 4xx; a failed audit write is
     logged at Error rather than turned into a 500 that would make the sender retry.
   - Kafka: the audit write must succeed before the record goes to the DLQ and its
     offset is stored. Otherwise the record is retried.

**Idempotent by EventId.** Outbox and Kafka redelivery are at-least-once, so
`IAuditTrail.RecordAsync` ignores ids it already has. Producers generate the id when
the fact happens. Kafka rejections derive it from `topic:partition:offset`, so a retry
maps to the same row.

**Append-only is enforced by the database, not by convention.** A trigger rejects
UPDATE, DELETE and TRUNCATE on the table. The API is read-only (admin role only):
`GET /api/v1/admin/audit/events` (search) and
`GET /api/v1/admin/audit/transactions/{id}/lineage`.

**Channel travels with the delivery.** `InboxMessage.Channel` stores how a delivery
arrived, so processing-time events (which run later, in the inbox dispatcher) carry the
same channel as the receipt. A channel filter therefore returns the whole story.

## Consequences
- Audit rows appear after the outbox poll interval (5 s by default), not instantly.
  `RecordedAt` vs `OccurredAt` shows the lag.
- Volume is roughly one row per inbound transaction, plus a few per delivery. There is
  no retention or partitioning yet (ADR-0006). A purge has to be a deliberate act that
  disables the trigger, which is intended; see docs/data-retention.md.
- Not audited: webhook bodies the framework rejects before our code runs (e.g.
  syntactically invalid JSON is a 400 from model binding). Rate-limited (429) calls are
  also not audited.
- The outbox dispatcher lives in the Transactions module, so it is the one component
  that knows about `AuditOutbox.MessageType`. A future module producing audit facts
  needs its own outbox dispatch, or a shared one if the outbox becomes a
  building-block service.
