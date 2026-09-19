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
internal module boundaries — enforced, where the extraction below has actually
happened, at the project/compiler level
(`TransactionAggregation.Tests/Architecture/ModuleIsolationTests.cs`), not just by
architecture tests over shared assemblies. Not by network boundaries.

**This ADR originally claimed the module boundaries below were already real.**
They weren't: a later audit found only *logical* boundaries inside four shared
projects (`TransactionAggregation.Domain/Application/Infrastructure/Persistence`),
enforced solely by `LayerDependencyTests.cs` — which checks horizontal layering
(Domain must not depend on Infrastructure) and enforces nothing about one module
reaching into another's. In practice it hadn't: ~10 handlers were found directly
querying another module's entities through the one shared `IApplicationDbContext`
(e.g. the Account module querying `Transactions` directly, `Customer.AddAccount()`
constructing `Account` entities itself). That gap is being closed by an actual
physical restructuring, phased to keep each step reviewable — see
[ADR-0009](0009-schema-per-module-database-strategy.md) for the accompanying
database strategy.

**Actual state, phase 1 (this review):**
- **WebhookSources** (`Modules/WebhookSources`) — fully extracted: own project, own
  `WebhookSourcesDbContext`/`webhooksources` schema, references only `SharedKernel`.
  Chosen first because the audit confirmed it had zero existing coupling to any
  other module — proves the pattern at the lowest risk.
- **BuildingBlocks.Messaging** — the Inbox/Outbox reliability mechanism, extracted
  as a genuinely generic shared building block (own project, own
  `MessagingDbContext`/`messaging` schema, no reference to any business module) that
  every module may depend on, the same way every module may depend on `SharedKernel`.
- **Customers/Accounts, BankLink, Transactions** — **still logical boundaries only**,
  living in the original shared `TransactionAggregation.Domain/Application/
  Infrastructure/Persistence` projects, pending later extraction phases. Do not read
  this ADR as claiming they're physically isolated yet — `ModuleIsolationTests.cs`
  only asserts isolation for the modules actually extracted so far. Accounts/Customers
  extraction in particular requires first fixing `Customer.AddAccount()` (Customer's
  aggregate currently owns Account-creation logic that belongs to Account).

Layering is enforced top-down (API → Application → Domain, with Infrastructure and
Persistence depending inward, never the reverse) via `TransactionAggregation.Domain`
having zero references to EF Core, ASP.NET Core, or provider-specific packages.

## Consequences
- A module can be extracted into its own service later **if** a genuine scaling or
  ownership reason appears (e.g. ingestion volume grows to need independent scaling
  from the read API). For the modules actually extracted so far, the physical
  project/DbContext boundary makes that extraction closer to mechanical; for the
  rest, it still requires the same restructuring this phase demonstrates.
- Until then, we get one process to deploy, one health check surface, one set of
  logs/traces to correlate, and no distributed-transaction problems between
  modules — directly serving the "low operational and maintenance complexity"
  requirement.
- The risk we accept: module boundaries can still shift as the domain is better
  understood. This is preferable to committing to network boundaries prematurely.
- Restructuring one module at a time, verified green before moving to the next,
  is deliberately slower than doing it all at once — the trade-off is a reviewable
  diff at each step instead of one large change with a long stretch where nothing
  compiles.

## Alternatives considered
- **Full microservices per the reference diagram** — rejected: no independent
  scaling/deployment need exists yet; would add Kafka, service mesh, per-service
  observability, and distributed-transaction handling for no measured benefit.
- **Single unstructured project ("big ball of mud")** — rejected: would not
  prevent the domain from becoming coupled to EF Core or provider-specific
  contracts, which the brief explicitly calls out as a failure mode to avoid.
