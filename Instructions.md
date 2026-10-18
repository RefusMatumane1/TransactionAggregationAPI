# Engineering Instructions

How to work in this repository: what the rules are, which ones a test enforces, the commands to run,
and the recipe for each common change. [`Goal.md`](Goal.md) says what the system guarantees;
this file says how to change it without breaking those guarantees.

Paths are relative to `TransactionAggregationAPI/` (the solution root, next to
`TransactionAggregationAPI.slnx`) unless noted. Tests live in `TransactionAggregation.Tests/`.

If a rule below conflicts with what you were asked to do, say so before changing anything. The
rules exist because each one has a failure behind it.

---

## 1. Orientation

```
TransactionAggregationAPI/              solution root
├── TransactionAggregationAPI/          API host: auth, rate limiting, middleware, Development seeding
├── TransactionAggregation.Worker/      Worker host: ALL background work lives here, nowhere else
├── TransactionAggregation.Hosting/     Composition shared by API and worker; rules JSON files
├── TransactionAggregation.MockAggregator/   Development only: mock-bank feeds through the real pipeline
├── TransactionAggregationUI/           Blazor WebAssembly front end (nginx in production)
├── TransactionAggregationAPI.AppHost/  .NET Aspire local orchestration
├── TransactionAggregationAPI.ServiceDefaults/  OpenTelemetry, health checks, secrets file loading
├── BuildingBlocks/                     Shared technical code; depends on no module
│   ├── SharedKernel/                   Domain primitives, Result/Error, ValueObject, Money-adjacent helpers
│   ├── BuildingBlocks.Application/     CQRS interfaces, MediatR pipeline, keyset pagination, log redaction
│   ├── BuildingBlocks.Persistence/     DbContext base, module registration, design-time support
│   ├── BuildingBlocks.Messaging/       Inbox, outbox, archive, failure classification (`messaging` schema)
│   └── BuildingBlocks.Web/             Result → ProblemDetails, route groups, policy/role/claim names
├── Modules/
│   ├── Transactions/    (schema `transactions`)         ledger, ingestion processing, read model, queries
│   ├── Customers/       (schema `customerdirectory`)    customers and their linked accounts
│   ├── WebhookSources/  (schema `webhooksources`)       the banks: keys, display names, signing keys
│   └── Audit/           (schema `audit`)                append-only audit trail
│       each: Domain · Application · Infrastructure · Presentation · Contracts
├── k8s/  keycloak/  monitoring/  perf/  scripts/  docker-compose*.yml  deploy-k8s.sh
└── TransactionAggregation.Tests/       Unit · Integration (Postgres, Kafka, Redis, MockBanks) · Architecture · Contract
```

Dependency direction inside a module: `Presentation → Application → Domain`, and
`Infrastructure → Application → Domain`. Modules reach each other only through `*.Contracts`.
`Presentation` is referenced only by the API host.

Data flow, in one line: **webhook or Kafka → inbox → (worker) normalize, categorize, record →
ledger + outbox + audit in one transaction → Kafka `transaction-events`; separately, the worker
rebuilds `DailyTotals` for the aggregate endpoints.**

## 2. Rules, and what enforces them

### Domain and data

1. **The ledger is insert-only.** Never write an `UPDATE`, `DELETE` or `TRUNCATE` against
   `transactions."Transactions"` or `audit."AuditEvents"`, and never add an endpoint that edits a
   transaction or its category. The database refuses them (triggers; grants revoked from `tagg_app`).
   A correction is a new entry. *Enforced by:* the triggers and grants, `LeastPrivilegeRolesTests`,
   `ProcessingGuaranteesPostgresTests`.
2. **Money is `decimal` through `Money`.** Non-zero, at most 4 decimal places, below 10^15, ISO 4217
   currency. Never `double` or `float`; never add amounts of different currencies.
3. **Dates are UTC** in the domain and the database. Bank-local times are converted during
   normalization using the bank's time zone. Reporting days are South African calendar days (UTC+2).
4. **Uniqueness is the database's job.** Do not replace a unique index with a check-then-insert.
   The ledger key is `(SourceName, ExternalAccountId, SourceExternalId)`; the inbox key is
   `(SourceName, IdempotencyKey)`. Handle the unique violation instead of preventing it.
5. **Nothing stores a customer on a transaction.** Customers link (bank, account id) pairs and are
   resolved on read. Do not add a `CustomerId` to the ledger.
6. **Categorization happens once, at recording time.** Do not recategorize stored rows.

### Boundaries

7. **Modules talk only through `*.Contracts`.** No project reference from one module's
   Domain/Application/Infrastructure/Presentation to another module's internals.
   *Enforced by:* `ModuleIsolationTests`, `AuditModuleIsolationTests`, `LayerDependencyTests`.
8. **Domain stays pure.** No EF Core, MediatR, validation or logging framework, and no ASP.NET
   reference. *Enforced by:* `DomainPurityTests`, `LayerDependencyTests`.
9. **Presentation holds HTTP only**, one file per endpoint under `Endpoints/`, wire shapes under
   `Requests/` and `Responses/`. Endpoint handlers are not `public`. Application DTOs are mapped to
   a response type, never serialized directly. Presentation does not reference Infrastructure, and
   Infrastructure does not map endpoints. *Enforced by:* `PresentationLayerTests`.
10. **The API runs no background service; the worker runs all of them.** Do not add a
    `BackgroundService` to the API host, and do not load Kafka there. *Enforced by:* `WorkerHostTests`.
11. **Naming:** `*CommandHandler`, `*QueryHandler`, interfaces start with `I`, domain entities are
    `sealed`, value objects inherit `ValueObject`. *Enforced by:* `LayerDependencyTests`.

### Messaging

12. **The inbox "processed" mark, the ledger rows, the outbox events and the audit events commit in
    one transaction.** Do not split them, and do not publish to Kafka from the request or inbox path;
    write an outbox row and let the outbox dispatcher publish.
13. **Classify every failure.** Permanent failures (malformed input, validation, domain violation,
    most 4xx) dead-letter at once; transient ones retry. Use `FailureClassifier`; do not catch and
    swallow. Retries are bounded: 5 attempts, exponential backoff with jitter, 300 s ceiling.
14. **Integration events are versioned and additive.** `TransactionRecorded` is v1
    (`Transactions.Contracts`). Add fields compatibly; a breaking change is a new schema version,
    published alongside the old until consumers move.
15. **Consumers deduplicate.** Delivery is at least once; `message-id` is the stable id.

### Security

16. **Every endpoint declares its policy.** Reads use `AuthorizationPolicies.Staff`, changes and
    admin routes use `AuthorizationPolicies.Admin`; the only unauthenticated routes are the webhook
    (API key), health, and Development-only Scalar.
17. **Scope every read by `IUserContext.InstitutionAccess`.** A transaction or customer outside the
    caller's banks is a `404`, indistinguishable from a missing one. A new query that forgets this is
    a BOLA bug. *Verify with:* `InstitutionAccessApiTests`, `CustomerApiTests`.
18. **Secrets never enter source.** Use `secrets.json` (git-ignored; shape in `secrets.template.json`),
    `.env` for Compose, or the Vault-rendered file. A required setting that is missing must stop the
    host at startup, not fall back to a default.
19. **Bank API keys are stored as SHA-256 hashes and shown once.** Never log or return key material.
20. **Errors are safe.** Return ProblemDetails with fixed text; log the exception. Never put stack
    traces, SQL, connection strings or internal type names in a response.

### Logging

21. **Structured templates only; no personal data.** Mask account ids with `LogRedaction.Account(...)`.
    Never log customer names, tokens, keys, webhook URLs or whole request objects. Do not push a
    request object into the log context. *Enforced by:* `LoggingHygieneTests`.

### Migrations

22. **A migration only changes schema, and never blocks writes.** From migration
    `20261003000000` on: no `DropTable`, no `DropColumn`, no data `UPDATE`/`DELETE`/`TRUNCATE`, and
    every index built with `CREATE INDEX CONCURRENTLY` through `migrationBuilder.Sql(..., suppressTransaction: true)`.
    Use expand/contract: add, backfill outside migrations, switch readers, drop in a later release.
    *Enforced by:* `MigrationPolicyTests`.
23. **Every model change ships with a migration**, in the module that owns the `DbContext`.
    *Enforced by:* the CI step "Migrations in sync with the model".
24. **Production never migrates from a pod.** Only Development applies migrations at start; elsewhere
    the `db-migrate` Job runs `--migrate-only` as `tagg_migrator` before the API and worker roll out.
    The running services connect as the DML-only `tagg_app`.

### Dependencies

25. **NuGet versions live only in `Directory.Packages.props`;** `.csproj` files list names without
    versions. Add a package only if .NET does not already provide the capability. CI fails on a known
    vulnerable package.

## 3. Commands

```bash
# from TransactionAggregationAPI/ (the solution root)
dotnet restore TransactionAggregationAPI.slnx
dotnet format TransactionAggregationAPI.slnx --verify-no-changes
dotnet build TransactionAggregationAPI.slnx -c Release -warnaserror
dotnet test TransactionAggregation.Tests -c Release      # needs Docker: Testcontainers starts PostgreSQL, Redis, Kafka

dotnet test TransactionAggregation.Tests --filter "FullyQualifiedName~Architecture"   # fast boundary check

# run the system (pick ONE stack; Compose and Aspire both publish Keycloak on 8081)
cp .env.template .env                      # once, fill in credentials (git-ignored)
docker compose up --build                  # add --profile tools and/or --profile monitoring
dotnet run --project TransactionAggregationAPI.AppHost    # Aspire; needs each secrets.json filled in
scripts/smoke-test.sh http://localhost:5001
```

In Development the API applies migrations, seeds demo accounts and customers, and registers the mock
banks as sources. `TransactionAggregation.MockAggregator` then pushes new transactions every 30 s
through the real webhook (or Kafka with `Feed__Channel=Kafka`).

Logins (local realm import): `staff@test.com` (FNB and StandardBank only) and `admin@test.com`
(everything). Their passwords come from `.env` or the AppHost's `secrets.json`; none is committed.

Ports: UI 7200, API 5001, Keycloak 8081, mock aggregator 5090, Seq 5341, Kafka UI 8083 (tools),
Prometheus 9090 and Grafana 3000 (monitoring). The API also serves `/metrics` on port 9464 only.

## 4. Recipes

### Add or change a categorization keyword
Edit `TransactionAggregation.Hosting/categorization-rules.json` (`CategorizationRules:Keywords`:
`"phrase": "CategoryName"`). Matching is whole-word, case-insensitive, longest phrase first. No code
change. Add a case to `Unit/Services/TransactionCategorizationServiceTests.cs`. A new *category* is
different: add the enum member (the next free integer; a check constraint covers the range, so it
needs a migration that widens the constraint), then update `categorization-rules.json`,
`normalization-rules.json` maps and the UI badge.

### Teach the pipeline a bank's format
Banks differ only in `normalization-rules.json` (production) or `normalization-rules.Development.json`
(mock banks). Add an entry under `Normalization:Institutions:<BankCode>` with `TimeZone`,
`DescriptionPrefixes` and `CategoryMap`; `Default` applies to every bank. The bank's transaction id
is never altered. Cover it in `Unit/Services/TransactionNormalizerTests.cs`.

### Add a bank
Create it through `POST /api/v1/admin/webhook-sources` (or the UI's Banks page): the code is fixed,
the key is shown once. For Kafka, register its ECDSA P-256 public key with
`PUT /admin/webhook-sources/{id}/signing-key`. Nothing in code changes. Staff reach it only if the
bank code is in their Keycloak `institutions` attribute.

### Add a read endpoint
1. Put a query record and handler in the module's `Application/Features/<Area>/Queries/…`
   (`IQuery<T>`, `…QueryHandler`), with a FluentValidation validator for its inputs.
2. Take `InstitutionAccess` from `IUserContext` and apply it in the query. Use `AsNoTracking`.
3. For a list, use the keyset paginator (`IKeysetPaginator`), a sort backed by an existing index,
   and the standard response `{ items, pageSize, nextCursor, hasMore, totalCount, totalCountCapped }`.
   A new filter or sort needs an index justified by the query; build it `CONCURRENTLY` (rule 22).
4. If it is cacheable, implement `ICacheableQuery` and choose a scope from `TransactionCacheScopes`.
   Ledger scope is invalidated by the outbox, the aggregates scope by the refresh.
5. Add one file under `Presentation/Endpoints/`, map it in the module's `*Endpoints.cs` with the right
   policy, return a Presentation response type, and convert `Result` with the helpers in `BuildingBlocks.Web`.
6. Tests: an application unit test, an API test for roles and out-of-scope access, and a Postgres
   test if the query shape matters.

### Add a write endpoint
Admin only. Use a command and `…CommandHandler` returning `Result`. Write the audit event **in the
same transaction** as the change (see `Customers.Application/Common/CustomerAudit.cs` and
`Transactions.Application/Common/Audit/InboundAudit.cs`), including the acting admin. Never audit
secrets or personal names. Add the event type to `Audit.Contracts/AuditEventTypes.cs`.

### Add a database change
```bash
# from the solution root; the design-time factory only needs a connection-string value
export ConnectionStrings__transactiondb="Host=localhost;Database=dev;Username=dev;Password=dev"
dotnet ef migrations add <Name> \
  -p Modules/<Module>/<Module>.Infrastructure -s Modules/<Module>/<Module>.Infrastructure \
  --context <Module>DbContext
```
(`MessagingDbContext` lives in `BuildingBlocks/BuildingBlocks.Messaging`.) Then check the generated
file against rule 22: replace any generated `CreateIndex` with a `CONCURRENTLY` SQL statement and
`suppressTransaction: true`, use `AddCheckConstraintWithoutBlockingWrites` for constraints on large
tables, and write a Down that restores the shape. Check with
`dotnet ef migrations has-pending-model-changes` and add a test under `Integration/Postgres/`
(`SchemaContractTests` pins the indexes and constraints).

### Add an outbound event or an outbox handler
Define the contract in the owning module's `*.Contracts` with an `EventType` and `SchemaVersion`.
Enqueue an `OutboxMessage` in the same transaction as the change. Add an `IOutboxMessageHandler` in
the worker (see `Outbox/TransactionRecordedHandler.cs`); if it changes cached data, invalidate the
cache scope through the handler run, not by hand. A handler that calls an external system must
throw `PermanentDeliveryException` for errors that retrying cannot fix.

### Add a background job
Derive from `PollingBackgroundService` in the worker. Give it an options class, a bounded batch,
retry with `DelayAfterFailure`, and a metric. A job that several replicas may run needs a lock
(see the advisory lock in `PostgresDailyTotalsRefresher`) or claim-based work (`FOR UPDATE SKIP
LOCKED`, as in `MessagingDbContext`). Register it only in the worker host.

### Add a metric or an alert
Use prometheus-net (`Metrics.CreateCounter/Gauge/Histogram`) with snake_case names ending in
`_total` or `_seconds`, and low-cardinality labels (bank code is fine; account or transaction ids
are not). Alert rules live in `monitoring/prometheus-rules.yml` (CI validates them with promtool);
Grafana dashboards are generated by `monitoring/grafana/generate_dashboards.py`, so edit the script,
not the JSON.

### Add configuration
Bind it to a strongly typed options class with a `SectionName`. Non-secret values go in
`appsettings*.json` (and `k8s/configmap.yaml` / `k8s/worker/configmap.yaml` when they differ in a
cluster); secrets go in `secrets.template.json` (shape only), `.env.template` and `k8s/secrets.yaml`
placeholders. Required values must fail at startup when absent, and numeric limits should be range-checked at
startup too (see `IngestionOptions`, `Ingestion:MaxTransactionsPerDelivery`, which the API and the
worker must agree on).

## 5. Behaviour you will otherwise get wrong

- **Webhook acceptance is not recording.** The webhook returns `202` once the delivery is in the inbox;
  the worker records it up to about 5 s later. Tests that need the ledger must run the dispatcher
  (`Helpers/InboxDispatcherTestExtensions.cs`) or poll.
- **Aggregates lag.** They come from `DailyTotals`, rebuilt every 5 minutes, and carry `asOf`. A new
  transaction appears in the list first and in totals later. Do not "fix" this with a request-time
  `GROUP BY`.
- **`pending` is not recorded.** Only posted transactions become ledger rows; the posting arrives
  later as its own item.
- **A repeated id is a skip, not an error.** Duplicates inside a batch or already in the ledger are
  skipped, counted, audited and alerted; the rest of the batch is recorded.
- **The same idempotency key with a different payload is `422`,** not a duplicate.
- **Redis is optional at runtime but required in configuration** outside Development (it holds the
  Data Protection key ring and the shared cache). When it is unreachable: reads go uncached,
  rate limits apply per replica, `/readiness` stays `200`, events still publish.
- **Staff with no banks see nothing;** a signed-in user with neither role gets a "No access" page.
- **Deactivating a bank stops it on both channels.**
- **Outbox ordering is not guaranteed within a batch.** Do not write a consumer that depends on arrival order.

## 6. Before you finish a change

1. `dotnet format --verify-no-changes`, then `dotnet build -warnaserror`, then `dotnet test`.
2. The architecture, logging-hygiene and migration-policy tests are green.
3. Every new endpoint has a policy, institution scoping, a validator and tests for 401, 403 and an
   out-of-scope id.
4. Every new write path has an audit event in the same transaction, and no new log line carries
   personal data.
5. Every new query is paginated by keyset and has an index (or a stated reason it does not need one).
6. Every new failure path says whether it is transient or permanent, and its retries are bounded.
7. If behaviour a README or `Goal.md` describes has changed, update the document in the same change.
   A document that disagrees with the code is a defect.

## 7. Do not introduce

Microservices, a saga, a generic repository over EF Core, partitioning, row-level security, a second
cache, or a new message broker, unless the problem they solve has been shown in this system. Each is
rejected today for a recorded reason in [`Goal.md`](Goal.md#8-non-goals-and-known-limits). Likewise
do not add `UPDATE`/`DELETE` paths to the ledger or audit trail, request-time aggregation,
background services in the API host, or secrets in any tracked file.
