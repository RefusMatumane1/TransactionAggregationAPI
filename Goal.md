# Transaction Aggregation Platform — Goal

This document states what the system is for, the guarantees it makes, and where each guarantee is
implemented and tested. It describes the code as it is. Anything the system does **not** do is
listed under [Non-goals and known limits](#8-non-goals-and-known-limits), not implied.

Paths are relative to `TransactionAggregationAPI/` (the solution root) unless noted.
Test paths are under `TransactionAggregation.Tests/`.

---

## 1. The brief

> *Build a system that aggregates customer financial transaction data from multiple mock data
> sources and categorizes the transactions. It will need an extensive API for retrieving
> aggregated information.*

How the system reads each phrase:

| Phrase | Meaning in this system |
|---|---|
| **Customer** | The person whose money it is. A customer holds bank accounts at several banks; an administrator links them. Customers are data, not users: nobody signs in as a customer. |
| **Multiple mock data sources** | Four mock banks (FNB, Absa, Capitec, Standard Bank). Each is a *source* with its own API key and its own wire format, delivered by REST webhook or signed Kafka record. `TransactionAggregation.MockAggregator` produces their feeds (Development only). |
| **Aggregates** | Totals by customer, bank, account, category and period, one currency at a time, or converted into one with configured rates (`convertTo`), served from a read model the worker rebuilds every 5 minutes. |
| **Categorizes** | Keyword rules in `TransactionAggregation.Hosting/categorization-rules.json`, applied once when a transaction is recorded. |
| **Extensive API** | A versioned REST API under `/api/v1`: transactions (list, detail, search, filters, keyset paging), four aggregate views plus a summary, the same views per customer, and administration of banks and customers. |

## 2. Purpose

A .NET 10 modular monolith that receives posted bank transactions, deduplicates, normalizes and
categorizes them, records them in an **insert-only PostgreSQL ledger**, publishes each one as an
integration event, and serves them through a secured REST API with aggregates.

The aim is not feature count. It is a small system whose correctness, security, resilience and
operational behaviour can be shown with evidence: each guarantee below names the code that provides
it and the test that checks it.

Principles that decide trade-offs:

- **The database enforces correctness.** Uniqueness, immutability, valid currency, valid category
  and non-zero amounts are constraints, triggers and grants, not conventions.
- **At-least-once delivery is assumed**; duplicates are normal and handled.
- **Simple beats clever.** Every component below has a stated reason; optional ones are marked.
- **Never authoritative outside PostgreSQL.** Redis caches and rate-limits; Kafka transports.
- **Nothing in the ledger is edited or deleted.** A correction would be a new entry.

## 3. Architecture

One deployable API, one background worker, five business modules, one PostgreSQL database.

```
Mock banks ── webhook (X-Api-Key) ──▶ API ──┐
           └─ Kafka bank-transactions ─▶ Worker (consumer, signature check) ──┤
                                                                               ▼
                                                              messaging.InboxMessages
                                                                               │ claim (SKIP LOCKED)
                                                                               ▼
                                              Worker inbox dispatcher: normalize → categorize → record
                                                                               │  ONE database transaction
                              ┌────────────────────────┬───────────────────────┴──────────┐
                              ▼                        ▼                                  ▼
                   transactions.Transactions   messaging.OutboxMessages           audit.AuditEvents
                     (insert-only ledger)                │
                              │                          ▼
                              │              Worker outbox dispatcher ──▶ Kafka transaction-events
                              ▼
               Worker daily-totals refresh ──▶ transactions.DailyTotals ──▶ API aggregate reads
```

| Part | Role | Required? |
|---|---|---|
| PostgreSQL 17 | System of record: ledger, inbox, outbox, read model, audit, customers, banks | Yes |
| API (`TransactionAggregationAPI`) | Webhook ingress and the REST API. Runs no background service. | Yes |
| Worker (`TransactionAggregation.Worker`) | Kafka consumer, inbox and outbox dispatchers, daily-totals refresh, message archiving. Scales apart from the API. | Yes |
| Kafka | Inbound signed bank records; outbound `TransactionRecorded` events | Publishing needs it; the inbound consumer is optional (`Kafka:Enabled=false`) |
| Redis | Response cache, distributed rate limits, Data Protection key ring. Never authoritative. Outside Development the API and worker refuse to start without a Redis connection string; at runtime they degrade rather than fail. | Configured always; runtime-optional |
| Keycloak | OIDC identity provider; the API only validates its JWTs | Yes |
| Seq, Prometheus, Grafana | Logs, metrics, dashboards, alert rules (`monitoring/prometheus-rules.yml`) | Optional |
| Aspire, Docker Compose, Kubernetes manifests, Vault | Three ways to run it, and how secrets are rendered in a cluster | Optional; Compose alone runs everything |

### Modules

Each module has its own PostgreSQL schema, its own `DbContext` and its own migrations. Modules
talk only through `*.Contracts` projects, enforced by architecture tests
(`Architecture/ModuleIsolationTests.cs`, `AuditModuleIsolationTests.cs`).

| Module | Schema | Owns |
|---|---|---|
| Transactions | `transactions` | Ingestion processing, the ledger, the `DailyTotals` read model, transaction and aggregate queries (including per customer), the `TransactionRecorded` v1 contract |
| Customers | `customerdirectory` | Customers and the (bank, account id) pairs linked to them; resolves a customer to its accounts for other modules |
| WebhookSources | `webhooksources` | The banks: one source per bank with its API-key hash, display name, colour and Kafka signing public key |
| Notifications | `notifications` | Where each bank's duplicate alerts go: its alert URL (stored encrypted), the default from configuration, and the sending of the alert. Exposes `IDuplicateAlertNotifier`; the worker's outbox handler hands it duplicates |
| Audit | `audit` | The append-only audit trail |

Shared technical code lives in `BuildingBlocks/` and depends on no module: `SharedKernel` (domain
primitives, `Result`/`Error`), `BuildingBlocks.Application` (CQRS abstractions, MediatR pipeline,
keyset pagination), `BuildingBlocks.Persistence`, `BuildingBlocks.Messaging` (inbox, outbox,
archive; `messaging` schema) and `BuildingBlocks.Web` (Result → ProblemDetails, route groups,
policy and role names). `TransactionAggregation.Hosting` is the composition shared by API and worker.

Each module is layered Domain → Application → Infrastructure, with a `Presentation` project holding
its HTTP surface. Only the API references Presentation; the worker runs every module without it.

## 4. Guarantees

Each row: the guarantee, what provides it, and the test that checks it.

### 4.1 Correctness of the ledger

| Guarantee | Provided by | Checked by |
|---|---|---|
| A ledger entry is never updated, deleted or truncated | Trigger `TR_Transactions_InsertOnly` and `TR_Transactions_NoTruncate`; `UPDATE`, `DELETE`, `TRUNCATE` revoked from `tagg_app` (migration `MakeLedgerInsertOnly`) | `Integration/Postgres/ProcessingGuaranteesPostgresTests.cs`, `Integration/Postgres/LeastPrivilegeRolesTests.cs`, `Architecture/MigrationPolicyTests.cs` |
| One ledger row per bank, account and bank transaction id | Unique partial index `IX_Transactions_Ledger_Key` on `(SourceName, ExternalAccountId, SourceExternalId)` where booked | `Integration/Postgres/IngestionIntegrityTests.cs`, `Integration/Postgres/ConcurrentIngestionTests.cs` |
| Money is exact and never zero | `Money` value object: `decimal`, ≤ 4 decimal places, \|amount\| < 10^15, 3-letter currency code (which codes are accepted is `currencies.json`, see 4.6); column `numeric(19,4)`; `CK_Transactions_Amount_NonZero`, `CK_Transactions_Currency_Iso4217` | `Unit/Domain/MoneyValueObjectTests.cs`, `Integration/Postgres/SchemaContractTests.cs` |
| Category and status are valid | `CK_Transactions_Category_Defined`, `CK_Transactions_Status_Defined` | `Integration/Postgres/SchemaContractTests.cs` |
| Only posted transactions are recorded | `pending` notices are acknowledged, audited as `transaction.pending_skipped`, and skipped | `Unit/Application/Commands/LedgerIngestionTests.cs` |
| Timestamps are UTC | `Transaction` rejects a non-UTC booking date; normalization converts bank-local times using the bank's time zone (default `Africa/Johannesburg`) | `Unit/Domain/TransactionEntityTests.cs`, `Integration/MockBanks/MockBankFormatsTests.cs` |

### 4.2 Idempotency and delivery

| Guarantee | Provided by | Checked by |
|---|---|---|
| A redelivered webhook call or Kafka record creates no second inbox row | Unique `(SourceName, IdempotencyKey)` on the inbox; key is the `Idempotency-Key` / `idempotency-key` header, or the SHA-256 of the payload | `Integration/WebhookApiIntegrationTests.cs`, `Integration/Postgres/InboxIdempotencyTests.cs` |
| The same key with a different payload is refused, never treated as a duplicate | `422 Inbox.IdempotencyKeyReused` (dead-lettered on Kafka) | `Integration/WebhookApiIntegrationTests.cs` |
| A crash after commit cannot duplicate or lose effects | The ledger rows, outbox events, audit events and the inbox "processed" mark commit in one transaction; a crash before commit leaves the message claimable again | `Integration/Postgres/ProcessingGuaranteesPostgresTests.cs`, `Integration/Postgres/StaleClaimReclaimTests.cs` |
| Two workers racing on the same delivery cannot both insert | Unique-violation → `MaxConflictAttempts` (3) re-checks, then defer to inbox retry | `Integration/Postgres/ConcurrentIngestionTests.cs` |
| Kafka offsets move only after the record is stored or dead-lettered | `EnableAutoOffsetStore=false`; the offset is stored after settlement | `Integration/Kafka/KafkaConsumerBrokerTests.cs`, `Unit/Kafka/BankTransactionsKafkaMessageHandlerTests.cs` |
| Outbound events are at-least-once with a stable id | Outbox row written with the ledger entry; headers carry `message-id` for consumer deduplication | `Integration/Kafka/KafkaIntegrationEventPublisherTests.cs`, `Unit/BackgroundServices/OutboxDispatchTests.cs` |
| Every duplicate is counted and alerted without failing the batch | `inbound_duplicates_total{source_name,level}`; `DuplicateInboundDetected` outbox message | `Unit/Application/Commands/ProcessInboundTransactionsCommandHandlerTests.cs`, `Unit/BackgroundServices/OutboxDispatchTests.cs` |

### 4.3 Authentication and authorization

| Guarantee | Provided by | Checked by |
|---|---|---|
| Only valid Keycloak tokens reach the API | JWT bearer: issuer, audience, lifetime and signing key validated; HTTPS metadata required outside Development; missing Keycloak settings stop startup | `Integration/RoleAccessTests.cs`, `Integration/OpenApiSecuritySchemeTests.cs` |
| `staff` is read-only and limited to its banks; `admin` reads everything and manages | Policies `Staff` (staff or admin) and `Admin`; `InstitutionAccess` built from the `institutions` claim; staff with no banks see nothing | `Integration/RoleAccessTests.cs`, `Integration/InstitutionAccessApiTests.cs` |
| No cross-bank access by manipulating IDs (BOLA/IDOR) | Every read path applies the caller's institution scope; an out-of-scope transaction or customer is `404`, identical to a missing one | `Integration/InstitutionAccessApiTests.cs`, `Integration/CustomerApiTests.cs` |
| A bank can only write for itself | Its API key (SHA-256 hash stored, shown once) selects the source; a payload naming another bank is `400` at the webhook or dead-lettered and audited | `Integration/WebhookApiIntegrationTests.cs`, `Integration/WebhookSourceAdminApiIntegrationTests.cs` |
| Kafka records are authentic | ECDSA P-256 signature over source, idempotency key and payload hash (`KafkaRecordSignature`, version `tagg-kafka-record-v1`) checked against the bank's registered public key; failures are dead-lettered and audited as `unauthenticated` | `Contract/KafkaRecordSignatureContractTests.cs`, `Unit/Kafka/BankTransactionsKafkaMessageHandlerTests.cs` |
| Reading a customer's data is itself audited | `CustomerViewAuditFilter` records `access.customer_viewed` before the response is returned; if the write fails the read fails; 404s record nothing | `Integration/CustomerApiTests.cs`, `Integration/AuditApiIntegrationTests.cs` |

### 4.4 Abuse resistance and safe errors

| Guarantee | Provided by | Checked by |
|---|---|---|
| Request volume is bounded | Redis-backed fixed windows per client (JWT `sub`, else IP): 200/min global, 150/min per endpoint group; `429` with `Retry-After`. If Redis is unreachable each replica enforces the same limits on its own. | `Unit/Platform/PlatformHardeningTests.cs`, `Integration/ApiHardeningIntegrationTests.cs`, `Integration/Redis/RedisIntegrationTests.cs` |
| Credential guessing is throttled before tokens are validated | `AuthFailureThrottleMiddleware`: an IP with more than 60 `401`s in a minute gets `429` | `Unit/Platform/PlatformHardeningTests.cs` |
| Request bodies are bounded | Kestrel `MaxRequestBodySize` 1 MiB; at most `Ingestion:MaxTransactionsPerDelivery` transactions (default 500, allowed 1–5000) per webhook call or Kafka record; server header removed | `Integration/ApiHardeningIntegrationTests.cs` |
| Clients never see stack traces or internals | `GlobalExceptionHandler` returns fixed ProblemDetails text and a `traceId`; details are logged | `Integration/ApiFailureSafetyTests.cs` |
| Browser-facing headers | `nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: no-referrer`, COOP; API responses add `default-src 'none'` CSP and `Cache-Control: no-store`; HSTS outside Development | `Integration/ApiHardeningIntegrationTests.cs` |
| No secrets or personal data in logs | `LogRedaction` masks account ids; `SensitiveDataDestructuringPolicy`; customer names stay out of logs and audit | `Architecture/LoggingHygieneTests.cs`, `Integration/RequestLoggingTests.cs` |
| No secrets in source | Untracked `secrets.json` locally, `.env` for Compose, Vault-rendered file in clusters; `deploy-k8s.sh` refuses placeholder secrets; gitleaks in CI | CI `Secret Scan` job |

### 4.5 Resilience

| Dependency | Failure behaviour |
|---|---|
| PostgreSQL | EF Core retry policy; transient inbox failures are retried with backoff; `/readiness` fails so traffic stops |
| Redis | Reads run uncached, rate limits fall back per replica, events still publish; `/readiness` stays `200` (Redis degrades it only) |
| Kafka (inbound) | Transient failures retry the same record with exponential backoff (cap `Kafka:MaxRetryBackoffSeconds`, 60 s) and pause the partition; records that can never succeed go to `bank-transactions.dlq` with `dlq-*` headers |
| Kafka (outbound) | Idempotent producer, `acks=all`, 30 s message timeout; failures are retried by the outbox |
| Duplicate-alert webhook | Posted to the duplicating bank's own URL (set per bank by an admin, stored encrypted with Data Protection, https to a public host only), else to the configured default, else only logged. Retried by the outbox on 408/429/5xx, dead-lettered on other 4xx; if the key ring cannot decrypt it (for example Redis is down) the attempt is transient and retried. A URL can embed a token, so it is write-only through the API and never logged |

Failures are classified in `BuildingBlocks.Messaging/FailureClassifier.cs`:
**permanent** (malformed JSON, domain violations, validation, most 4xx, SQL state classes 22 and 23
other than unique violations) dead-letter immediately; **transient** retry. Retries are bounded:
inbox and outbox allow 5 attempts, with `RetryBackoff` doubling from 4 s up to a 300 s ceiling and
jittered between half and full of the ceiling. A claim older than 10 minutes is reclaimed; a
reclaimed message already at 5 attempts is dead-lettered as abandoned. On shutdown the worker
releases unworked claims within 5 s (`ClaimRelease`). Tests: `Unit/Resilience/ResiliencePolicyTests.cs`,
`Unit/BackgroundServices/FailureClassificationTests.cs`, `Unit/Messaging/*`, `Integration/Postgres/StaleClaimReclaimTests.cs`.

### 4.6 Read performance

| Guarantee | Provided by |
|---|---|
| Aggregates never run on the request path | `transactions.DailyTotals`, keyed by bank, account, day, category and currency, rebuilt by the worker every `Aggregation:IntervalMinutes` (5) |
| A late or re-delivered entry cannot be double counted | Each refresh recomputes in full every account-day touched since the last checkpoint, looking back `Aggregation:OverlapMinutes` (10); a single-flight `pg_try_advisory_xact_lock` keeps replicas from refreshing at once |
| Callers know how stale totals are | Every aggregate response carries `asOf` |
| A refresh is bounded | At most `Aggregation:MaxRowsPerRun` (500,000) new entries per run; a larger backlog is worked off in back-to-back runs |
| Page N costs the same as page 1 | Keyset (cursor) pagination on every list; default page 20, max 100 (audit: 200) |
| Totals are cheap and honest | `includeTotal` counts up to 10,000 and reports `totalCountCapped` beyond |
| Hot queries are index-backed | Partial indexes `WHERE Status = booked`: covering `(Date DESC, Id DESC)`, amount `(Amount, Id)`, account `(SourceName, ExternalAccountId, Date DESC, Id DESC)`, trigram GIN on `Description` for `searchTerm`; sorting is limited to `date` and `amount` for that reason |
| Currencies never mix by accident | An aggregate reads one `currency` (default: `Currencies:BaseCurrency`) unless `convertTo` is given |
| One source of truth for currencies | `currencies.json` (`Currencies:BaseCurrency`, `Currencies:Rates`) is the only list and the only base: ingestion, re-check at recording, `convertTo`, view defaults and demo data all read `ICurrencyCatalog`; `Money` holds only the 3-letter shape; `GET /currencies` publishes the table (tests: `Architecture/CurrencyConsistencyTests.cs`, `Unit/Currency/CurrencyCatalogTests.cs`) |
| Conversion is explicit, exact and reproducible | `Currencies:BaseCurrency` and `Currencies:Rates` (`currencies.json`, validated at start-up); `convertTo` converts per daily row in the database, rounded to four decimals, then sums; the response states the target, `ratesVersion`, the rates used and the currencies it could not convert; cache keys include `ratesVersion` |
| Only configured currencies are ingested | A delivery in a currency without a rate is refused with the list of supported ones; the ledger keeps what the bank sent and nothing is converted on the way in |
| Cached reads stay correct | Ledger reads cache 5 min and are invalidated by the outbox after a dispatch cycle that recorded something; aggregate reads cache up to 55 min and are invalidated when a refresh commits. If invalidation fails, entries expire on their own. |

Tests: `Integration/Postgres/TransactionAggregatesPostgresTests.cs`,
`Integration/Postgres/DailyTotalsRefresherPostgresTests.cs`, `Integration/Postgres/CursorPaginationPostgresTests.cs`,
`Integration/Redis/RedisIntegrationTests.cs`.

### 4.7 Operability

| Guarantee | Provided by |
|---|---|
| Health semantics are deliberate | `/liveness` does no dependency I/O; `/readiness` checks PostgreSQL (Redis degrades only, Seq never counts); `/health` lists every check. The worker serves the same three. |
| Metrics are scraped privately | prometheus-net on port 9464, never routed publicly; alert rules cover API down, error rate, p99 latency, memory, worker down, inbox/outbox backlog and dead-lettering, ingestion lag, Kafka dead letters and consumer crashes, stale aggregates, and failing background jobs |
| Every request is traceable | `X-Correlation-Id` on every response (caller's if a short `[A-Za-z0-9._-]` token, else the trace id); trace id on ProblemDetails; `traceparent` and `correlation-id` carried through inbox, outbox and Kafka headers |
| Every inbound delivery has a trail | `audit.AuditEvents`, append-only by trigger and by revoked grants; written in the same transaction as the change it describes |
| Schema changes are safe to ship | Per-module migrations; Development applies them at start, elsewhere a `db-migrate` Job (`--migrate-only`) runs as schema owner `tagg_migrator` before any replica rolls; the app connects as DML-only `tagg_app`; `MigrationPolicyTests` fails the build for any migration from `20261003000000` on that drops a table or column, changes data, or builds an index without `CONCURRENTLY` (earlier migrations predate the policy); CI fails on model changes without a migration |
| Message tables stay small | Processed inbox and outbox rows older than 30 days (`MessageArchive:RetainDays`) are moved to archive tables in batches of 1,000 |

## 5. Interfaces

All routes are under `/api/v1`. Reads need `staff` or `admin`; changes and admin routes need `admin`.

| Area | Routes |
|---|---|
| Transactions | `GET /transactions`, `GET /transactions/{id}`, `GET /transactions/summary` |
| Aggregates | `GET /transactions/aggregates/{institutions, categories, cash-flow, comparison}` |
| Currencies | `GET /currencies` (staff): the base currency and every supported currency with its rate |
| Banks | `GET /banks` (staff); `/admin/webhook-sources` list, create (optionally with the bank's `notificationUrl`), update, rotate key, register signing key, activate, deactivate (admin) |
| Customers | `GET /customers`, `/customers/{id}`, `/customers/{id}/transactions`, `/customers/{id}/aggregates/{cash-flow, categories, institutions, comparison}` (staff); `POST /customers`, `POST /customers/{id}/accounts`, `DELETE /customers/{id}/accounts` (admin) |
| Notifications | `GET`, `PUT /admin/notifications/duplicate-alert-webhooks/{bankCode}` (admin) |
| Audit | `GET /admin/audit/events`, `GET /admin/audit/transactions/{id}/lineage` (admin) |
| Ingestion | `POST /webhooks/bank-aggregator/transactions` (API key); Kafka topic `bank-transactions` |
| Events out | Kafka topic `transaction-events`: `TransactionRecorded` v1, keyed `{institution}:{externalAccountId}` |
| Operations | `/liveness`, `/readiness`, `/health`; `/metrics` on port 9464 only; `/openapi/v1.json` (authorized outside Development); Scalar at `/scalar/v1` in Development only |

Every list returns `{ items, pageSize, nextCursor, hasMore, totalCount, totalCountCapped }`.
Errors are RFC 9457 ProblemDetails with per-field `errors` for validation failures. The wire formats
are specified in [`TransactionAggregationAPI/README.md`](TransactionAggregationAPI/README.md#api-reference).

## 6. Domain facts

- **Transaction** is immutable once recorded: id, external account id, `Money`, description,
  category, `TransactionSource` (bank code plus the bank's transaction id), booking date (UTC),
  status, recorded-at, and a metadata dictionary holding the bank's original description and
  category. Only `Booked` is ever written; `Pending` and `Expired` exist as retired values for old rows.
- **Categories** (stored as integers, enforced by check constraint): Uncategorized 0, Groceries 1,
  Dining 2, Transportation 3, Entertainment 4, Utilities 5, Housing 6, Healthcare 7, Income 8,
  Transfer 9, Shopping 10, Subscriptions 11.
- **Categorization** (`TransactionCategorizationService`): lower-cased whole-word phrase match
  against the rules file; the longest keyword wins, ties broken ordinally; otherwise the bank's own
  category (mapped through the bank's `CategoryMap`); otherwise `Income` for a positive amount and
  `Uncategorized` for the rest. It is decided once, at recording time, and never revisited.
- **Normalization** (`TransactionNormalizer`, rules in `normalization-rules.json`): date to UTC,
  description trimmed and stripped of known statement prefixes, currency upper-cased, bank category
  mapped. The bank's transaction id is never changed. Originals are kept in metadata.
- **Customer**: reference (unique, 1–50 chars), name, and at most 50 linked accounts, each a
  (bank, account id) pair stored in the bank's registered spelling. Links are resolved at read time,
  so linking or unlinking never touches the ledger and takes effect on history immediately.
- **Reporting day** is a South African calendar day (UTC+2, no daylight saving).

## 7. Definition of done

A change is done when, from a clean checkout:

1. `dotnet build -warnaserror` succeeds and `dotnet format --verify-no-changes` is clean.
2. `dotnet test` passes. About 780 cases run; integration tests use real PostgreSQL, Redis and Kafka
   through Testcontainers, so Docker must be running.
3. Every module's migrations match its model (`dotnet ef migrations has-pending-model-changes`).
4. Architecture, logging-hygiene and migration-policy tests pass, so module boundaries, log content
   and migration safety have not regressed.
5. The images build, run as a non-root user, and pass the Trivy gate (no CRITICAL with a fix).
6. `docker compose up --build` starts and `scripts/smoke-test.sh` passes.

CI (`.github/workflows/ci-cd.yml`) performs exactly these, plus gitleaks, an SPDX SBOM per image,
Kubernetes manifest validation (kubeconform) and alert-rule validation (promtool). A weekly OWASP
ZAP baseline scan runs against the Compose stack (`.github/workflows/zap-baseline.yml`). Dependabot
tracks NuGet packages, GitHub Actions and base-image digests.

## 8. Non-goals and known limits

Stated as failure and consequence.

- **Stale totals.** Aggregates lag the ledger by up to one refresh (5 minutes by default), and
  longer while a backlog above 500,000 entries is worked off. `asOf` states the age. A failed
  refresh retries after 30 s, doubling to the interval; `TransactionAggregatesStale` fires if none
  succeeds for two hours. The transaction list is fresher than the totals.
- **Ingestion latency.** Inbox and outbox are polled every 5 s, so a delivery is recorded up to
  about 5 s after it is accepted.
- **Event ordering.** The outbox publishes up to 16 events concurrently, so two events for one
  account in one batch can reach Kafka in either order. Consumers must order by
  `BookedAt`/`RecordedAt` and deduplicate on `message-id`.
- **Best-effort cache invalidation.** If Redis is unreachable when a transaction is recorded, the
  event is still published, and a ledger read cached just before the outage may be up to 5 minutes
  stale. Nothing is cached while Redis is down.
- **Staff scope lives in the application**, not in PostgreSQL row-level security, which would need
  the caller's banks set on every pooled transaction. A bug in a new query could bypass it; the
  institution-access tests exist to catch that.
- **Conversion uses static configured rates.** There is no FX feed. A rate applies at read time to all
  history, takes effect on restart, and is only as good as what was configured; the shipped rates
  are placeholders. A currency without a rate is left out of a converted view and reported in
  `conversion.unconvertedCurrencies`. Sorting and amount filters on the transaction list use the
  recorded amount, not the converted one, because the indexes are on it.
- **No partitioning, no saga, no microservices.** Volumes fit one table with the indexes above, no
  workflow spans separately committed steps, and the modules scale together. Revisit partitioning
  at roughly 100 million ledger rows.
- **Customers are not users.** There is no customer sign-in, self-service or registration.
- **Single-node infrastructure in the bundled manifests.** PostgreSQL, Redis, Kafka (KRaft, one
  broker, replication factor 1) and Keycloak run as single replicas in `k8s/`. That is a local
  cluster profile, not a high-availability design.
- **Deployment.** The CI deploy job is a notice and a post-deployment smoke test hook, not an
  automated rollout. Third-party images in the manifests are pinned by tag; the application images'
  base layers are pinned by digest.
- **Alert URLs are screened by name, not by DNS.** Setting a bank's alert URL (Notifications module) rejects non-https,
  credentials in the address, `localhost`, `.local`/`.internal` names and private, loopback and
  link-local IP literals. A public-looking hostname that resolves to a private address is not caught,
  so a malicious admin could aim the worker at an internal service. The cluster's network policy is
  the second line.
- **Mock data.** The bank formats in `normalization-rules.Development.json` are invented for the
  mock aggregator; real banks need their own profiles.
- **Retention.** Processed messages are archived after 30 days; no rule deletes ledger or audit
  rows, because the database forbids it. Regulatory retention periods are not encoded and must be
  confirmed with compliance before production.
