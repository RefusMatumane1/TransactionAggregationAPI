# ADR-0012: Kafka as an additional inbound channel (internal messaging stays polled)

## Status
Accepted. **Partially supersedes [ADR-0003](0003-polling-inbox-outbox-not-a-broker.md)**:
its rejection of a broker still holds for everything *inside* the process boundary
(outbox dispatch, inbox processing), but no longer for how providers *deliver* to us.

## Context
ADR-0003 rejected a broker because "no second service consumes these events yet". Its
own revisit trigger was a party outside our process that needs durable, ordered,
replayable delivery. That trigger fired on the *inbound* side: an aggregator (the mock
in `TransactionAggregation.MockAggregator`, standing in for a real one) publishes
bank-transaction batches to a Kafka topic, and replays from an offset are how it
recovers after an outage on either side. A webhook-only design would have made us
responsible for their retry policy, and would lose ordering within an account.

The broker is not ours by choice. It is the provider's delivery mechanism, and the
question is only how we consume it safely.

## Decision
- **Consume `bank-transactions` with one consumer group** in the worker host
  (`BankTransactionsKafkaConsumer`). Each record is translated into the **same**
  `ReceiveBankTransactionsCommand` the REST webhook sends, so validation, the inbox
  write, idempotency and audit are identical on both channels.
- **Offset committed only after the record is settled** (`EnableAutoOffsetStore = false`,
  `StoreOffset` after the inbox row or the DLQ record is durable). A crash or rebalance
  redelivers the record, and the redelivery lands on the same inbox row through the
  `(SourceName, IdempotencyKey)` unique index.
- **Transient failures stall the partition** with capped exponential backoff. Skipping
  ahead would commit past a record that was never stored. **Permanent failures**
  (malformed JSON, failed validation, an unknown source, a reused idempotency key
  carrying new content) go to `bank-transactions.dlq` with origin headers, and are audited.
- **A consumer that dies stops its host** (`Environment.ExitCode = 1`,
  `StopApplication`), so the orchestrator restarts it rather than leaving a green pod
  that no longer ingests. This is counted by `kafka_bank_transactions_consumer_crashes_total`.
- **Everything after the inbox stays as ADR-0003 describes**: Postgres-claimed polling
  inbox/outbox, no broker for internal integration events.

## Consequences
- The broker is an optional dependency: without `ConnectionStrings:kafka` the worker
  doesn't register the consumer, and the webhook path is unaffected.
- Ordering is per partition (keyed by external account id). Out-of-order delivery across
  partitions or channels is handled by the domain's pending → posted → expired state
  machine, not by the transport.
- Kafka has no per-record credential. The `source` header is trusted only as far as topic
  ACLs let a producer write, and only names a registered, active `WebhookSource`. That
  source's institution scope ([ADR-0013](0013-webhook-source-institution-scoping.md))
  bounds what a mislabelled record can reach.

## Alternatives considered
- **Webhook only.** Rejected: the provider publishes to Kafka, and replay-from-offset is
  their recovery story.
- **Using Kafka internally as well (outbox → topic).** Rejected for the same reasons as
  ADR-0003: there is still no second consumer of our integration events.
