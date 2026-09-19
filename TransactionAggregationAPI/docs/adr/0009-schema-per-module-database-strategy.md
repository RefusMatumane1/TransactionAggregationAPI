# ADR-0009: Schema-per-module database strategy (supersedes ADR-0008)

## Status
Accepted — supersedes [ADR-0008](0008-single-database-schema.md)

## Context
ADR-0008 kept every table in Postgres's default `public` schema, reasoning
that schema separation without a real per-schema database role/grant model
would be "cosmetic organization, not an actual security boundary" — and
warned against exactly the trap of pretending otherwise. That reasoning was
sound for the question it was answering at the time (does this repo need
schema separation for organizational/security reasons?) and **nothing about
that conclusion has changed**: this repo still uses one Postgres role/connection
for every context, so schemas still provide **no access-control boundary**
today. Anyone reading this repo should not infer otherwise from the schema
names introduced below.

What changed is the actual question. This session began restructuring the
codebase from four shared layer projects (`TransactionAggregation.Domain/
Application/Infrastructure/Persistence`) into true per-module projects (see
the updated [ADR-0001](0001-modular-monolith-not-microservices.md)) — and a
full audit found the existing single `IApplicationDbContext`, with every
entity's `DbSet<T>` exposed together, was the direct structural enabler of
~10 handlers doing cross-module queries that shouldn't have existed in a real
module boundary (e.g. the Account module directly querying `Transactions`,
the Customer module directly querying `Transactions`). A shared `DbContext`
makes that coupling trivial to write and easy to miss in review. Splitting
`DbContext`s per module — one for `WebhookSources`, one for the shared
`BuildingBlocks.Messaging` reliability infrastructure, with the remaining
business entities staying on the existing `ApplicationDbContext` until their
own modules are extracted in later phases — makes that class of coupling a
compile error instead of a code-review miss: a module's handler simply has
no `DbSet` to reach through.

Schema-per-module is a direct consequence of DbContext-per-module, not an
independent motivation: each DbContext needs its own EF Core migrations
history so one module's migration never touches another's, and Postgres's
own `__EFMigrationsHistory` bookkeeping plus table-name collisions make
separate schemas the simplest way to let three independent migration
histories coexist in one physical database without one context's migration
generator getting confused by tables it doesn't own.

## Decision
Each module gets its own `DbContext` and its own Postgres schema, all
against the **same physical database** (one connection string,
`ConnectionStrings:transactiondb`):

- `ApplicationDbContext` (`TransactionAggregation.Persistence`) — `public`
  schema, unchanged: `Customers`, `Accounts`, `Transactions`, `BankLinks`
  (still shared-project business entities pending later extraction phases).
- `MessagingDbContext` (`BuildingBlocks.Messaging`) — `messaging` schema:
  `InboxMessages`, `OutboxMessages`.
- `WebhookSourcesDbContext` (`Modules.WebhookSources`) — `webhooksources`
  schema: `WebhookSources`.

`ApplicationDbContext` and `MessagingDbContext` additionally share one
scoped `NpgsqlConnection` (registered once in `Program.cs`), not just the
same connection *string* — required so `ApplicationDbContext.SaveChangesAsync`
can open one real transaction and have `MessagingDbContext` participate in
it via `Database.UseTransactionAsync`. This preserves the Outbox pattern's
core guarantee (ADR-0003): a domain-event handler that writes an
`OutboxMessage` during `SaveChangesAsync` must commit atomically with the
business-entity change that raised the event, or the pattern is pointless.
`WebhookSourcesDbContext` has no such requirement — every write there is a
standalone unit of work — and keeps its own independent connection.

**Not verified against a live Postgres in this session** (no Docker
available) — migrations were generated (`dotnet ef migrations add`, which
doesn't require a reachable database) and reviewed by hand, but never
applied to a real database. The transaction-sharing path in particular needs
a real run before it's trusted: verify that a domain-event handler's Outbox
write and the triggering entity change either both commit or both roll back,
and that `EnableRetryOnFailure` + the manually-managed transaction
(`Database.CreateExecutionStrategy().ExecuteAsync`) doesn't throw the
"execution strategy does not support user-initiated transactions" error EF
Core is known to raise in this exact scenario if the wrapping is wrong.

## Consequences
- Module boundary violations that were previously a silent code-review miss
  (querying another module's `DbSet` through the shared context) are now a
  compile error — there is no `DbSet` to reach through.
- Three independent EF Core migration histories instead of one. A developer
  running `dotnet ef migrations add` must target the right project
  (`--project` / `--startup-project` / `--context`) — got materially harder
  to do by accident, but real if pointed at the wrong context.
- `MessagingDbContext` deliberately has no `EnableRetryOnFailure` (see the
  comment in `BuildingBlocks.Messaging/DependencyInjection.cs`): combined
  with a retrying `ApplicationDbContext` sharing its transaction, two
  independent retrying execution strategies on the same physical transaction
  is exactly the scenario EF Core's guard exists to prevent. The dispatcher
  background services (which use `MessagingDbContext` standalone, no shared
  transaction) lose automatic retry-on-transient-failure as a result — a
  deliberate, narrower trade-off, not an oversight.
- **Still no access-control boundary** — this is the one thing ADR-0008 got
  right that remains true. If a real per-schema role/grant model is ever
  introduced (ADR-0008's own "revisit when" condition), schema separation
  already being in place makes that meaningfully easier; until then, don't
  read these schema names as security boundaries.

## Alternatives considered
- **Keep ADR-0008's single shared context, enforce module boundaries by
  convention/code review only** — rejected: this is exactly the status quo
  the audit found failing in practice (~10 handlers had already drifted into
  cross-module queries despite the modular-monolith intent being documented
  since ADR-0001).
- **Per-module DbContext, same schema (just namespaced by table prefix)** —
  rejected: doesn't solve the "three migration histories in one schema"
  problem cleanly (EF Core's migrations history table and model snapshot
  are per-`DbContext`, and Postgres has no table-prefix-based access
  isolation anyway) — schema separation is the standard, low-friction way to
  let independent migration histories coexist.
- **Distributed transaction (`System.Transactions.TransactionScope`) instead
  of a shared connection** — rejected: Npgsql's classic MSDTC-based
  distributed-transaction promotion is not supported cross-platform,
  specifically not on Linux, which is this application's actual container
  deployment target (`k8s/api/deployment.yaml`). A shared connection with
  `UseTransactionAsync` achieves the same atomicity for two contexts against
  the same physical database without needing a transaction coordinator.
