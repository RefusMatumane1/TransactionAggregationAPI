# Transaction Aggregation API

A .NET 10 platform that receives posted bank transactions from the banks, deduplicates,
normalizes and categorizes them, records them in an insert-only PostgreSQL ledger, publishes each
one as an integration event, and serves them through a secured, versioned REST API with
aggregation endpoints for staff (scoped to their banks) and administrators. There are no
customer accounts, and nothing in the ledger is ever edited or deleted. It is a modular monolith: one deployable API, one
background worker, and modules with explicit boundaries.

The detailed guide is [`TransactionAggregationAPI/README.md`](TransactionAggregationAPI/README.md).
This page is the front door.

---

## What it does

- **Ingests** bank deliveries by REST webhook (one API key per bank) or Kafka (per-record ECDSA
  signature against the bank's registered public key). Both land in the same inbox.
- **Guarantees exactly-once effects under at-least-once delivery.** Deliveries are deduplicated
  by idempotency key or payload hash. Transactions are unique per institution, account and bank
  id, enforced by the database. The inbox's "processed" mark commits in the same transaction as
  the rows it produces.
- **Normalizes** each bank's format to one canonical model, and **categorizes** by configurable
  keyword rules.
- **Records postings only, immutably.** Pending notices are audited and skipped; each posting
  becomes one ledger entry. A trigger and the application role's grants make UPDATE, DELETE and
  TRUNCATE impossible, whoever asks.
- **Publishes** `TransactionRecorded` v1 to the Kafka `transaction-events` topic through the
  outbox, keyed by account, with trace and correlation headers.
- **Serves** a JWT-secured API (Keycloak; `staff` reads the banks in their `institutions` claim,
  `admin` reads all and manages) with cursor pagination on every list, index-backed sorting,
  filtering, and aggregates in one ISO 4217 currency at a time (by bank, account, category, cash
  flow, period comparison).
- **Audits** every inbound delivery, every ledger entry and every admin change (with its actor)
  in an append-only trail written in the same transaction as the change.

## Architecture in brief

```
Banks ─────webhook──▶ API ─┐
         ──Kafka────▶ Worker ─┴─▶ inbox (PostgreSQL) ──▶ Worker: normalize, categorize, store
                                                            │   ledger INSERT + outbox + inbox mark
                                                            │   + audit rows in one database transaction
                                                            ▼
                                        outbox ──▶ Kafka transaction-events, cache invalidation, alerts
Staff/admin ─JWT─▶ API ──▶ PostgreSQL (source of truth), Redis (cache and rate limits only)
```

| Module | Owns |
|---|---|
| Transactions | ingestion pipeline, the insert-only ledger, queries and aggregates, the `TransactionRecorded` contract |
| WebhookSources | the banks: one source per bank, its API key, display name and Kafka signing key |
| Audit | the append-only audit trail of deliveries, ledger entries and admin changes |

Each module has its own PostgreSQL schema and migrations. Modules talk to each other only
through `*.Contracts` interfaces, and architecture tests enforce that.

**Key decisions:**

- Modular monolith, not microservices.
- PostgreSQL is the system of record; Redis is never authoritative (cache and rate limits only).
- Polling inbox/outbox tables in PostgreSQL rather than an internal broker.
- Kafka as an additional inbound channel, with signed records.
- No saga; no partitioning yet.
- Least-privilege database roles: a schema-owning migrator and a DML-only application role.
- Cursor (keyset) pagination for every list.
- Staff-only aggregation: no customers or bank links.
- Each bank is a source; its key decides the bank.
- Insert-only ledger of posted transactions.
- Any ISO 4217 currency, one currency per aggregate.
- Untracked `secrets.json` locally, rendered by Vault in clusters.
- Staff see only their assigned institutions.
- `TransactionRecorded` integration events on Kafka.
- Migrations never change data, drop, or block writes.

---

## Quick start

Requires Docker Desktop. From `TransactionAggregationAPI/`:

```bash
cp .env.template .env                           # once: fill in the credentials (git-ignored)
docker compose up --build                       # the system
docker compose --profile tools up --build       # + pgAdmin, Redis Commander, Kafka UI
docker compose --profile monitoring up --build  # + Prometheus, Grafana
docker compose down -v                          # stop and wipe all data
```

In Development the API applies migrations and seeds demo accounts on start. Elsewhere, a gated
migration Job applies them before the API rolls out.

| Service | URL |
|---|---|
| UI | http://localhost:7200 |
| API | http://localhost:5001 (`/api/v1/...`) |
| API reference (Scalar, Development only) | http://localhost:5001/scalar/v1 |
| Health | `/liveness` (process), `/readiness` (PostgreSQL reachable), `/health` |
| Keycloak | http://localhost:8081 |
| Mock bank aggregator | http://localhost:5090 |
| Seq (logs) | http://localhost:5341 |

Demo logins and seed data are described in the detailed guide. No credential is committed:
compose reads them from `.env`, the hosts from a git-ignored `secrets.json` (Vault renders the
same file in clusters). For breakpoints and the Aspire dashboard, run
`TransactionAggregationAPI.AppHost`. For Kubernetes, use `deploy-k8s.sh`, which refuses to run
while `k8s/secrets.yaml` still holds placeholders. Both are covered in the detailed guide.

## Tests

```bash
cd TransactionAggregationAPI
dotnet test
```

684 test cases: unit, architecture, contract, API (in-process host), and integration against
real PostgreSQL, Redis and Kafka through Testcontainers, so Docker must be running.

CI (`.github/workflows/ci-cd.yml`) runs formatting, a warnings-as-errors build, a dependency scan,
a migration drift check, the tests, gitleaks, image builds with a non-root check, Trivy, an SBOM,
manifest validation and a compose smoke test.

## API

Every route is under `/api/v1`. Reads need the `staff` or `admin` realm role; changes and the
admin routes need `admin`; staff are confined to their banks. Every list returns
`{ items, pageSize, nextCursor, hasMore, totalCount, totalCountCapped }`; pass `nextCursor` back
as `cursor`. Endpoints and schemas are in the OpenAPI document (`/openapi/v1.json`, browsable
through Scalar in Development). The webhook payload, the Kafka record format (including how to
sign a record) and the `TransactionRecorded` event are in the detailed guide.

## Categorization

Rules live in `TransactionAggregation.Hosting/categorization-rules.json`, so adding a keyword
needs no code change. A keyword matches whole words in the description, and the longest matching
keyword wins. If nothing matches, the bank's own category is used. Failing that, positive amounts
are `Income` and the rest `Uncategorized`. The category is decided once, when the transaction is
stored, and never changes afterwards.

## Known limitations

- A Redis outage longer than about a minute dead-letters `TransactionRecorded` outbox messages:
  the handler invalidates the cache before publishing, and a Redis failure spends the message's
  retry budget. The ledger is unaffected; the dead-lettered messages must be requeued once Redis
  is back.
- The CI deploy stage is a notice, not an automated deployment.
- Third-party images are pinned by tag, not digest.
- Staff institution scope is enforced in the application, not by PostgreSQL row-level security
  (it would need the caller's banks set on every pooled transaction).
- No foreign-exchange conversion: aggregates never mix currencies, by design.
