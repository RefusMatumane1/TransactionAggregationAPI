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
  schedule against a staging environment — see [ADR-0006](../docs/adr/0006-no-partitioning-yet.md)
  and [ADR-0007](../docs/adr/0007-offset-pagination.md) for the query-shape
  assumptions this test is meant to validate.

## Assumptions and targets

These are starting assumptions, not measured production numbers — there is no
production traffic yet. Update them once real usage data exists.

| Assumption | Value | Why |
|---|---|---|
| Per-customer transaction volume | Low thousands, not millions | Consumer banking aggregation, not a payments processor. Informs ADR-0006 (no partitioning) and ADR-0007 (offset pagination). |
| Read:write ratio | Heavily read-skewed | Customers check balances/history far more often than new transactions arrive. |
| Target load | 20 concurrent virtual users sustained | A reasonable early-stage concurrent-user assumption; revisit once real traffic is observed. |
| `GET .../accounts` p95 latency | < 200ms | Simple indexed lookup, no aggregation. |
| `GET .../transactions/filter` p95 latency | < 300ms | Indexed (`CustomerId+Date+Category`/`CustomerId+Status`, see `TransactionConfiguration.cs`) paginated query — the query this system's indexing strategy is built around. |
| `GET .../transactions/summary` p95 latency | < 400ms | Aggregates a customer's full date-ranged transaction set in memory after querying — the most expensive read endpoint, and the first candidate for caching or a materialized view if it exceeds this target under real volume. |
| Error rate | < 1% | Anything higher under sustained load indicates a real problem, not noise. |

If a run misses these thresholds, that's a signal to profile the specific query
(check `EXPLAIN ANALYZE` against the actual index usage) before reaching for
caching, denormalization, or partitioning — see the "revisit when" sections in
the ADRs linked above.

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
  perf/k6/transactions-read.js
```

The script's `setup()` registers a fresh throwaway customer and account per run
so results aren't polluted by previous runs' data. It does **not** seed bulk
transaction history automatically (see the comment in `seedAccountAndTransactions`
in the script) — for a representative test of the filter/summary endpoints under
real data volume, seed transactions through the webhook ingestion endpoint (the
same path production traffic uses) against a linked bank account before running,
using `SEED_TRANSACTION_COUNT` as a guide for how many to generate.
