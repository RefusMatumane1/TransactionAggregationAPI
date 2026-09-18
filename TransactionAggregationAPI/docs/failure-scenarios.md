# Failure scenarios

Instructions.md section 42's named scenarios, each with detection, response,
recovery, data-consistency impact, observability, and user impact — grounded
in what the code actually does, verified by reading the relevant source
during this review (not inferred from the architecture alone).

## 1. PostgreSQL unavailable
- **Detection**: `/health` readiness probe fails (Postgres check has no
  `failureStatus` override, so it stays `Unhealthy` — correctly, since Postgres
  is the system of record and an outage really does mean the API can't serve
  most requests). `/alive` liveness stays healthy (self-check only).
- **Response**: Npgsql's `EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: 30s)`
  retries transient connection failures automatically before surfacing an error.
- **Recovery**: Automatic — once Postgres is reachable again, the readiness
  probe passes and k8s returns the pod to the load-balancer rotation with no
  manual intervention.
- **Data consistency**: No partial writes — EF Core wraps `SaveChangesAsync()`
  in an implicit transaction; a failed connection mid-write rolls back cleanly.
- **Observability**: Retry attempts and final failures are logged; OpenTelemetry
  traces show the failed DB span.
- **User impact**: 5xx / unavailable for any request touching the database
  until Postgres recovers.

## 2. Redis unavailable
- **Detection**: `/health` reports `Degraded` (not `Unhealthy` — see
  [ADR-0005](adr/0005-redis-cache-only.md)), so the pod **stays in rotation**.
- **Response**: `RedisCacheService` catches the exception and returns a cache
  miss; the rate limiter is configured `AllowRequestOnRedisFailure = true`
  (fails open, not closed).
- **Recovery**: Automatic on Redis returning; no state to reconcile since
  Redis is never the source of truth.
- **Data consistency**: Unaffected — financial data was never in Redis.
- **Observability**: Cache-miss/Redis-exception logs; OTel Redis instrumentation
  shows the failed calls.
- **User impact**: Slightly slower reads (DB instead of cache) for most
  operations. **Exception**: bank-link initiation depends on Redis for OAuth
  state and will legitimately fail until Redis recovers — a narrow, accepted
  scope, not an oversight.
- **Startup-time fix, this review**: if Redis-backed Data Protection key
  persistence can't be set up at all (no connection string, or a malformed
  one), the app now explicitly falls back to `UseEphemeralDataProtectionProvider()`.
  Previously the code logged that it would do exactly this and then didn't —
  leaving Data Protection on ASP.NET Core's implicit default, which in a
  container is ambiguous and could attempt a local file write that fails.
  Verified live: both the missing-connection-string and malformed-connection-
  string paths now start cleanly with no unhandled exception.

## 3. Provider (bank aggregator) unavailable
- **Detection**: Outbound HTTP calls through `IBankAggregatorClient` fail;
  the standard resilience handler's circuit breaker trips after repeated
  failures.
- **Response**: `AddStandardResilienceHandler()` applies retry + timeout +
  circuit-breaking centrally to every outbound `HttpClient`, including this
  one — no per-call resilience code needed.
- **Recovery**: The circuit breaker probes for recovery (half-open state)
  once its break duration elapses; automatic once the provider is reachable.
- **Data consistency**: No local state changes on failure — `CompleteBankLink`
  fails before any commit if the provider call throws.
- **Observability**: The exception now propagates to `ExceptionHandlingMiddleware`
  (traceId-bearing 500), rather than being masked as a generic error — see the
  handler-centralization fix earlier in this review.
- **User impact**: Bank-link completion fails until the provider recovers;
  existing linked accounts/transactions are unaffected.

## 4. Provider timeout
Same mechanism and impact as scenario 3 — the standard resilience handler's
timeout policy applies uniformly; a slow provider is treated the same as an
unreachable one once its budget is exceeded.

## 5. Provider returns 429
- **Detection/Response**: 429 is one of the transient statuses .NET's standard
  resilience handler retries by default, with exponential backoff and jitter.
- **Residual gap**: The default retry policy does not specifically parse or
  respect a `Retry-After` header from the provider — it backs off on its own
  schedule rather than the provider's requested schedule. Worth a targeted
  Polly policy for this specific client if 429s from a real provider turn out
  to be frequent enough to matter.

## 6. Provider returns malformed event
- **Webhook path**: request-body validation (`ReceiveBankTransactionsCommandValidator`)
  rejects malformed payloads with 400 before they're ever queued to the Inbox.
- **OAuth completion path**: a malformed provider response during
  `ExchangeAuthorizationCodeAsync`/`GetLinkedAccountAsync` throws during
  deserialization, which now propagates to the centralized exception handler
  (500 + traceId) instead of crashing unhandled.
- **User impact**: The specific request fails; no corrupt data is persisted
  either way, since validation/deserialization happens before any write.

## 7. Duplicate event
- **Detection/Response**: A database-level unique constraint on
  `(CustomerId, Source.ExternalId)` rejects the duplicate insert; the handler
  catches the resulting `DbUpdateException` (Postgres error 23505) and returns
  a no-op success rather than an error.
- **Data consistency**: Guaranteed by the database, not application logic —
  see [ADR-0002](adr/0002-postgresql-as-system-of-record.md).
- **Observability**: Logged as a warning (`"Duplicate transaction external id..."`),
  not an error — this is expected, not exceptional.
- **User impact**: None — the transaction appears exactly once regardless of
  how many times the same event is delivered.

## 8. Same event processed concurrently
This is scenario 7 under concurrency, and the same database constraint
resolves it the same way: both concurrent inserts race to commit, the
database allows exactly one and rejects the other with 23505, and the
rejected one is caught and treated as a harmless duplicate — the classic
"check-then-insert" race the brief calls out is avoided because there is no
separate check step for the final guarantee; the constraint is the guarantee.

**Now automated (this review)**: `ConcurrentIngestionTests.cs` (in the same
`Integration/Postgres/` suite) submits the identical external transaction ID
through two independent `DbContext`s racing to insert it, against a real
Postgres unique constraint, and asserts exactly one row results and neither
request errors. This is the first test in the whole suite to actually
exercise the handler's `catch (DbUpdateException ex) when
(ex.InnerException?.Message.Contains("23505") == true)` clause — the
in-memory provider used everywhere else can't produce a Postgres-specific
SQLSTATE code, so that catch clause had zero coverage until now.

## 9. Outbox publication fails
- **Detection**: The handler for a given `OutboxMessage` throws; caught in
  `OutboxDispatcherBackgroundService.ProcessMessageAsync`.
- **Response**: `MarkFailed` increments `Attempts` and sets exponential
  backoff (`min(2^(attempts+1), 300)` seconds) via `NextAttemptAt`.
- **Recovery**: Retried automatically on the next eligible poll; after
  `MaxAttempts` (default 5) the message is marked `DeadLettered` and stops
  being retried — it does not block other messages, since the dispatcher
  polls `Status = Pending`, not FIFO-blocking on one stuck message.
- **Data consistency**: Outbox side effects here are analytics tracking,
  notifications, and cache invalidation — not publishing to an external
  broker (see [ADR-0003](adr/0003-polling-inbox-outbox-not-a-broker.md)). A
  dead-lettered message means those specific side effects (e.g. a
  notification) never fire for that event; the underlying transaction data
  itself is unaffected.
- **Observability**: Each failure is logged with the message ID and type;
  dead-lettering logs a warning.
- **User impact**: A missed notification/analytics event at worst — never
  lost or incorrect transaction data.

## 10 & 11. Consumer crashes after/before database commit
- **Before commit**: Nothing was persisted (claim + processing changes are
  tracked in-memory until `SaveChangesAsync()`), so a crash here is
  equivalent to the message never having been picked up — safe, automatic
  retry on the next poll by any dispatcher instance.
- **After the claim commits but before the final `SaveChangesAsync()`**:
  **this was a real gap, fixed during this review.** `ClaimInboxMessagesAsync`/
  `ClaimOutboxMessagesAsync` mark a message `Processing` in their own
  immediately-committed `UPDATE ... RETURNING` statement — separate from the
  later `SaveChangesAsync()` that marks it `Processed`/`Failed`. A crash
  between those two points used to leave a message stuck in `Processing`
  forever, with no automatic recovery. Both claim methods now also reclaim
  messages that have been `Processing` longer than `ClaimTimeoutMinutes`
  (default 10 minutes, configurable per `InboxOptions`/`OutboxOptions`) —
  a standard visibility-timeout pattern. Traded-off risk: if a single batch
  genuinely takes longer than the timeout while the dispatcher is still
  alive (not crashed), it could theoretically be reclaimed and processed
  twice by two instances — acceptable given downstream idempotency (the
  unique-constraint dedup for inbound transactions) and the default timeout
  being far larger than a normal batch's processing time.
- **Verified against real PostgreSQL** (not just reasoned about): a message
  was manually inserted with `Status = Processing` and `ClaimedAt` 15 minutes
  in the past (simulating a crashed dispatcher), with no other pending
  messages in the table. Running the real app against a throwaway
  `postgres:16-alpine` container, the `OutboxDispatcherBackgroundService`
  claimed and processed it on its very next poll (`Claimed 1 outbox messages`)
  — the only way that could happen is via the new stale-claim reclaim branch,
  since the message was never `Pending`. It was then correctly dead-lettered
  (`Status = DeadLettered, Attempts = 1`) since its `Type` didn't match a
  known handler. The migration fix was verified the same way: running
  `--migrate-only` against a fresh database created all 8 tables and recorded
  all 11 migrations in `__EFMigrationsHistory`, and running the full app in
  `Production` mode confirmed it does *not* re-run migrations or seed data
  (the `IsDevelopment()` gate holds).
- **Now automated (this review)**: `TransactionAggregation.Tests/Integration/Postgres/StaleClaimReclaimTests.cs`
  converts the manual check above into a permanent, CI-enforced test —
  spins up a real `postgres:16-alpine` container via Testcontainers, inserts
  a message stuck in `Processing` past `ClaimTimeoutMinutes`, and asserts
  `ClaimOutboxMessagesAsync`/`ClaimInboxMessagesAsync` reclaim it (plus a
  companion test asserting a message *within* its claim window is correctly
  left alone). `MigrationTests.cs` in the same folder does the equivalent for
  the migration fix. This closes the "should-have" gap the production
  readiness checklist previously flagged.

## 12. Message broker unavailable
**Not applicable** — there is no message broker; see
[ADR-0003](adr/0003-polling-inbox-outbox-not-a-broker.md). The closest
equivalent failure is Postgres unavailability (scenario 1), since the
Inbox/Outbox tables live there.

## 13. Application restarts during processing
Covered by the same reclaim mechanism as scenarios 10/11 — a restart mid-batch
behaves identically to a crash: either nothing was committed (safe, automatic
retry) or a message is stuck `Processing` until the claim timeout elapses,
at which point another dispatcher instance (or the same one, post-restart)
reclaims it.

## 14. Database migration fails
- **Detection**: The dedicated `db-migrate` Job (`k8s/api/migration-job.yaml`)
  exits non-zero if `MigrateAsync()` throws. Note: the manifest carries
  `helm.sh/hook` annotations and its own header comment describes a Helm
  pre-install/pre-upgrade hook workflow, but **this repo's `k8s/helm/`
  directory has a `Chart.yaml` and per-environment `values.*.yaml` files with
  no `templates/` directory at all** — there is no Helm chart actually
  rendering these manifests, so the Helm-hook annotations do nothing today.
  The workflow that actually works, confirmed by the manifest's own "Apply
  workflow" comment, is plain `kubectl apply -f migration-job.yaml && kubectl
  wait --for=condition=complete ... && kubectl apply -f deployment.yaml`. This
  is a real gap between what the comments describe and what's deployable —
  see the production readiness checklist.
- **Fixed and verified live, this review**: the Job's container previously
  declared only `POSTGRES_PASSWORD` and `ConnectionStrings__transactiondb` —
  missing the Seq/Keycloak/Redis configuration `Program.cs` requires at
  startup regardless of `--migrate-only` (those are builder-time
  registrations that run before the migrate-only branch is even checked).
  Confirmed by running the actual container image with only the env vars the
  Job used to declare: it crashed with `Unable to add a Seq health check
  because the 'ServerUrl' setting is missing` before a single migration ran.
  Added `envFrom: transaction-api-config` plus explicit `ConnectionStrings__seq`/
  `ConnectionStrings__redis` mappings (mirroring `deployment.yaml`); re-ran
  against a real Postgres with exactly the fixed config set and confirmed
  migrations now apply successfully.
- **Also fixed, this review**: `k8s/configmap.yaml` hardcoded
  `ASPNETCORE_ENVIRONMENT: "development"` for every environment — since there's
  no Helm chart to override it per environment (see above), this meant the
  `IsDevelopment()` production-safety gate never actually protected a real
  deployment using these manifests. Flipped the default to `Production` (the
  repo owner explicitly chose to fix this immediate risk and leave building a
  real Helm chart as separate future work). This alone would have broken every
  authenticated request, since `RequireHttpsMetadata` was tied to the same
  `IsDevelopment()` check and the in-cluster Keycloak serves plain HTTP —
  decoupled it into its own explicit `Keycloak:RequireHttpsMetadata` config
  value (`Program.cs`), set to `false` in the ConfigMap with a comment
  explaining why (TLS terminates at the Ingress; NetworkPolicy isolates
  in-cluster traffic). Verified live: ran the real image with
  `ASPNETCORE_ENVIRONMENT=Production`, a plain-HTTP Keycloak authority, and
  `Keycloak__RequireHttpsMetadata=false` against real Postgres/Redis — clean
  startup, no exception, no seeding/auto-migration occurred.
- **Response**: A failed Job run blocks the operator from proceeding to the
  next `kubectl apply` step in the documented workflow (there's no automated
  gate enforcing this today since it isn't a real Helm hook — an operator
  following the documented steps manually respects the ordering; nothing
  currently stops someone from applying `deployment.yaml` without first
  waiting on the Job).
- **Recovery**: Forward-only — fix the migration (or the underlying data
  issue) and re-run the Job. There is no automatic rollback of a partially
  applied migration; EF Core migrations are not natively transactional across
  multiple DDL statements on all databases, so recovery is a manual,
  case-by-case forward fix, consistent with the brief's "forward-only
  migrations" guidance.
- **User impact**: The new version never reaches production; the previous
  version keeps serving traffic uninterrupted (rolling deployment never
  starts) — *provided* the operator actually followed the manual
  Job-then-Deployment ordering, since nothing enforces it automatically.

## 15. Partial provider outage
Verified `HttpBankAggregatorClient`: this system integrates with a single
third-party aggregator service (Plaid/Yodlee-style) that itself fronts
multiple banks — `Institution` (FNB, Absa, ...) is passed as a request
parameter to that one aggregator's API, not a separate per-bank endpoint this
system calls directly. There is exactly one downstream HTTP dependency here,
so the standard resilience handler's circuit breaker being scoped to the
single `HttpClient` is correct, not a gap — there is no per-institution
isolation to provide because there is no per-institution endpoint. If the
aggregator's own backend has trouble reaching one specific bank while staying
up for others, that surfaces as institution-specific request failures (retried
individually per the standard policy), not as a circuit-breaker scoping
concern on our side.

## 16. Large transaction volume
- **Ingestion**: webhook payloads are capped at 500 transactions per request
  (tested — `ReceiveTransactions_MoreThanFiveHundredTransactions_Returns400`),
  preventing a single oversized payload from overwhelming a batch.
- **Read side**: composite indexes (`CustomerId+Date+Category`,
  `CustomerId+Status`) keep per-customer paginated queries efficient; offset
  pagination was a deliberate choice with a documented revisit threshold — see
  [ADR-0007](adr/0007-offset-pagination.md) and
  [ADR-0006](adr/0006-no-partitioning-yet.md) for when that assumption should
  be revisited.

## 17. Poison message
A message that fails deterministically (e.g. a payload the handler can never
successfully process) retries with backoff up to `MaxAttempts`, then is
marked `DeadLettered` and stops being retried — it does not block the queue,
since other pending messages are picked up independently. There is currently
no alerting specifically on dead-lettered messages accumulating — worth
adding a metric/alert (`inbox_messages_dead_lettered_total` /
`outbox_messages_dead_lettered_total`) if poison messages turn out to be a
real operational concern.

## 18. Invalid authentication
- **Customer-facing API**: JWT validation (issuer, audience, lifetime, signing
  key, per `Program.cs`) rejects invalid/expired/wrong-audience tokens with
  401.
- **Webhook ingestion**: a missing, wrong, or deactivated-source API key
  returns 401 (tested — `WebhookApiIntegrationTests`).
- **User impact**: Immediate, correct rejection; no partial processing occurs
  for an unauthenticated request.

## 19. Unauthorized customer access
Covered extensively by the IDOR/BOLA checks in `AccountEndpoints.cs` and the
regression tests added during this review (`AccountApiSecurityTests`) — a
caller cannot access another customer's accounts by editing either the
customerId or accountId segment of the URL; both cases return 404, not a
data leak. See the [threat model](threat-model.md) section 2 for the full
analysis and residual risk.
