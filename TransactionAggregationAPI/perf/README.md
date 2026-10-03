# Performance testing

Instructions.md section 26 asks for a performance-testing strategy with measurable
targets, not premature optimization. This is that strategy: a documented set of
assumptions and thresholds, plus a runnable [k6](https://k6.io) script — not a
CI gate on every PR.

## Why k6, and why not blocking CI

- **k6** over NBomber/BenchmarkDotNet: this is a black-box HTTP load test against a
  running instance (realistic workload, real network/serialization/DB round trips),
  not an in-process microbenchmark. BenchmarkDotNet is the right tool if the
  categorization engine's hot loop is ever suspected of being a bottleneck — it
  isn't today, so it isn't included.
- **Not wired into the required PR pipeline**: a load test needs a running app,
  Postgres, Redis, and Keycloak, and takes minutes — running it on every push would
  slow every PR for a signal that's only meaningful against realistic data volumes
  and infrastructure, which CI's ephemeral containers don't represent well. Instead,
  run it manually before a release, after a change to a hot query path, or on a
  schedule against a staging environment, to validate the query-shape assumptions
  below.

## Assumptions and targets

These are starting assumptions, not measured production numbers — there is no
production traffic yet. Update them once real usage data exists.

| Assumption | Value | Why |
|---|---|---|
| Transaction volume | Hundreds of thousands, not hundreds of millions | Aggregation across a bounded set of provider accounts, not a payments processor. Why the table is not partitioned, and why every list uses cursor pagination. |
| Read:write ratio | Heavily read-skewed | Staff browse dashboards and lists far more often than deliveries arrive. |
| Target load | 20 concurrent virtual users sustained | A reasonable early-stage concurrent-user assumption; revisit once real traffic is observed. |
| `GET /transactions` p95 latency | < 300ms | Keyset-paginated over the `Date DESC, Id DESC` covering index — the query this system's indexing strategy is built around. |
| `GET /transactions/summary` p95 latency | < 400ms | Aggregates in PostgreSQL (`GROUP BY` month and category), so the result is at most ~120 month buckets whatever the history length; the period is capped at 10 years. |
| `GET /transactions/aggregates/{providers,categories}` p95 latency | < 400ms | `GROUP BY` in PostgreSQL over one reporting period (12 months by default). |
| Error rate | < 1% | Anything higher under sustained load indicates a real problem, not noise. |

If a run misses these thresholds, that's a signal to profile the specific query
(check `EXPLAIN ANALYZE` against the actual index usage) before reaching for
caching, denormalization, or partitioning — see the "revisit when" sections in
the ADRs linked above.

## Measured on every CI run

The load test needs a production-like environment. What can be measured without one is
measured in the test suite, against real PostgreSQL:

| Measurement | Result | Test |
|---|---|---|
| Transaction list page (`ORDER BY Date DESC, Id DESC LIMIT 21`, first page and a deep keyset page) at 50,000 rows / 200 providers | index scan on `IX_Transactions_Date_Id_Covering`, no sequential scan or sort, < 50 ms (`EXPLAIN ANALYZE`) | `ProcessingGuaranteesPostgresTests.HistoryAndExpiryQueries_UseTheirIndexes_AndStayFastAtVolume` |
| Amount sort, account statement and description search at the same volume | partial indexes `IX_Transactions_Amount_Id`, `IX_Transactions_Account_Date_Id`, `IX_Transactions_Description_Trgm`; no sequential scan, < 50 ms | same |
| Summary and balances | computed by SQL aggregation; memory bounded by bucket count | `AggregationPostgresTests` |
| Cache invalidation | O(1) (`INCR` of a scope version), no keyspace scan | `RedisIntegrationTests` |

## Running it

Requires [k6](https://k6.io/docs/get-started/installation/) and the full local
stack (API + Postgres + Redis + Keycloak) running via `docker-compose up` or
`dotnet run --project TransactionAggregationAPI.AppHost`.

The script authenticates using a dedicated `transaction-perf-test` Keycloak client
(`keycloak/realm-export.json`) with direct-grant (password) auth enabled — this
mirrors the approach the main README documents for calling the API directly
without a browser. This client exists only in the local/dev realm export; it is
never present in a production Keycloak realm.

```bash
k6 run \
  -e BASE_URL=http://localhost:5001 \
  -e KEYCLOAK_URL=http://localhost:8081 \
  -e STAFF_PASSWORD="$TRANSACTION_APP_STAFF_PASSWORD" \
  perf/k6/transactions-read.js
```

The script's `setup()` signs in as the dev staff user (`staff@test.com`, or `-e STAFF_EMAIL=...`).
`STAFF_PASSWORD` is required. The password is not committed anywhere, so take it from your
`.env` (compose) or from `Parameters:app-staff-password` in the AppHost's `secrets.json`. It reads whatever data the environment holds:
the Development seed and the mock aggregator's feed give a realistic starting volume; for a
heavier test, push more deliveries through the webhook ingestion endpoint (the same path
production traffic uses) before running.
