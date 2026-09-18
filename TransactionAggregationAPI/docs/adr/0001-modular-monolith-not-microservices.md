# ADR-0001: Modular monolith instead of microservices

## Status
Accepted

## Context
The system aggregates financial transactions from multiple providers, categorizes
them, and exposes them through a REST API. It is easy to imagine a "textbook"
microservices decomposition (ingestion service, normalization service,
categorization service, aggregation service, notification service, ...), and the
reference diagram this project started from (`IdealArchitecture.png`) shows exactly
that.

Microservices would only pay for themselves if we had:
- Independent scaling needs per capability (we don't — ingestion volume and read
  volume both scale with customer count, not independently),
- Independent deployment cadence per team (there is one team),
- A genuine need to run different tech stacks per capability (there isn't), or
- Bounded contexts stable enough that the module boundaries are unlikely to move
  (they are still being discovered — see the actual module list below).

None of these hold today. A microservices split before boundaries are proven adds
network calls, distributed transactions, duplicated infrastructure, and operational
surface area (service discovery, per-service CI/CD, per-service observability) with
no corresponding business or scaling benefit.

## Decision
Build a **modular monolith**: one deployable ASP.NET Core process, with strong
internal module boundaries enforced by architecture tests
(`TransactionAggregation.Tests/Architecture/LayerDependencyTests.cs`), not by
network boundaries.

Actual module boundaries in the codebase, organized by bounded concern rather than
by the "textbook" list in the original brief:
- **Customers/Accounts** — customer identity and account ownership.
- **BankLink** — provider OAuth linking lifecycle (initiate/complete/revoke).
- **Transactions** — ingestion (`ProcessInboundTransactions`), categorization,
  querying, export, summarization.
- **WebhookSources** — managing which external systems may push transactions in,
  and their API keys.
- **Audit/Outbox/Inbox** — cross-cutting reliability concerns used by the above.

Layering is enforced top-down (API → Application → Domain, with Infrastructure and
Persistence depending inward, never the reverse) via `TransactionAggregation.Domain`
having zero references to EF Core, ASP.NET Core, or provider-specific packages.

## Consequences
- A module can be extracted into its own service later **if** a genuine scaling or
  ownership reason appears (e.g. ingestion volume grows to need independent scaling
  from the read API). The internal boundaries already make that extraction
  mechanical rather than a rewrite.
- Until then, we get one process to deploy, one health check surface, one set of
  logs/traces to correlate, and no distributed-transaction problems between
  modules — directly serving the "low operational and maintenance complexity"
  requirement.
- The risk we accept: module boundaries can still shift as the domain is better
  understood. This is preferable to committing to network boundaries prematurely.

## Alternatives considered
- **Full microservices per the reference diagram** — rejected: no independent
  scaling/deployment need exists yet; would add Kafka, service mesh, per-service
  observability, and distributed-transaction handling for no measured benefit.
- **Single unstructured project ("big ball of mud")** — rejected: would not
  prevent the domain from becoming coupled to EF Core or provider-specific
  contracts, which the brief explicitly calls out as a failure mode to avoid.
