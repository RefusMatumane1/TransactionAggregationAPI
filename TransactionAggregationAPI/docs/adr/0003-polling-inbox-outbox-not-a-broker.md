# ADR-0003: Polling-based Inbox/Outbox instead of a message broker

## Status
Accepted

## Context
The brief asks us to evaluate whether Kafka, RabbitMQ, or Azure Service Bus is
appropriate, and explicitly warns: *"Do not introduce a message broker solely
for architectural theatre."*

A broker earns its place when there are genuinely independent producers and
consumers that need durable, ordered, replayable delivery across process/service
boundaries — e.g. multiple independently-deployed services reacting to the same
event stream. Today, transaction ingestion and its downstream effects
(persisting the transaction, emitting an integration event) happen inside the
same modular monolith. There is no second service on the other end of a topic.

## Decision
Implement **Inbox** and **Outbox** as PostgreSQL tables with polling background
dispatchers (`InboxDispatcherBackgroundService`, `OutboxDispatcherBackgroundService`
in `TransactionAggregation.Infrastructure/BackgroundServices`), not a message
broker.

- **Outbox** (`OutboxMessage`, indexed on `(Status, NextAttemptAt)`): transaction
  persistence and outbox-row insertion happen in the same database transaction
  (see `ProcessInboundTransactionsCommandHandler`), so a crash between "save
  transaction" and "publish integration event" cannot happen — the event row is
  already committed atomically with the data it describes. The dispatcher polls
  and publishes with backoff/dead-lettering built into the state machine.
- **Inbox** (`InboxMessage`): same polling/backoff/dead-letter shape, for
  idempotent consumption of anything arriving from outside the process boundary
  (e.g. webhook-delivered events).

## Consequences
- We get at-least-once delivery, retry with backoff, and dead-lettering — the
  properties a broker would give us — without running Kafka/RabbitMQ/ASB
  locally or in production. This directly serves "keep local infrastructure
  manageable" and "low operational and maintenance complexity."
- Latency from insert to dispatch is bounded by the poll interval, not
  instantaneous push. This is an acceptable trade for a system whose ingestion
  is itself not sub-second-latency-sensitive (transaction sync is not a
  real-time trading feed).
- **Revisit this decision if**: a second independently-deployed consumer needs
  to react to the same integration events (i.e. the modular monolith actually
  gets split per ADR-0001), or ingestion volume grows to the point where
  polling latency/throughput becomes the bottleneck. Either condition is a
  concrete, measurable trigger — not a guess.

## Alternatives considered
- **Kafka / RabbitMQ / Azure Service Bus** — rejected for now: no second service
  consumes these events yet, so a broker would add operational surface
  (cluster/queue management, another moving part in local dev via Aspire/Docker)
  without a corresponding capability we don't already have via polling
  Inbox/Outbox.
