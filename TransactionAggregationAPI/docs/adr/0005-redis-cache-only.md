# ADR-0005: Redis is cache-only, never the source of truth

## Status
Accepted (see also the readiness-probe fix this ADR justifies, in
`TransactionAggregationAPI.ServiceDefaults/Extensions.cs`)

## Context
Redis is fast and convenient for ephemeral data, but it is not the durability
guarantee financial transaction data needs. The brief is explicit: *"Do not use
Redis as the source of truth for financial transactions."*

## Decision
Redis is used only for:
- Response/query caching (`RedisCacheService`, used by `CachingBehavior`),
- Bank-link OAuth state (short-lived, `InitiateBankLinkCommandHandler`,
  10-minute TTL),
- Rate-limiting counters (`RedisFixedWindowRateLimiter`),
- Data Protection key storage (so encrypted bank-link tokens survive restarts
  across replicas — itself cache-adjacent infrastructure, not transaction data).

It is never used to store transactions, accounts, or customer records — those
live only in PostgreSQL (ADR-0002).

Because Redis is optional infrastructure by this definition, the application
must stay correct when Redis is unavailable:
- `RedisCacheService` catches Redis exceptions and returns a cache-miss rather
  than propagating the failure (`RedisCacheService.cs`).
- The rate limiter is configured with `AllowRequestOnRedisFailure = true` — a
  Redis outage fails open on rate limiting rather than blocking all traffic.
- The health check for Redis is registered with `failureStatus:
  HealthStatus.Degraded` (not `Unhealthy`), so a Redis outage reports as
  Degraded (HTTP 200) rather than failing the `/health` readiness probe (HTTP
  503) and pulling otherwise-healthy pods out of rotation. Before this ADR, the
  Redis check had no explicit `failureStatus`, which meant the default
  (`Unhealthy`) applied and a Redis blip could cascade into every pod being
  marked not-ready — exactly the "cascading failure from an optional
  dependency" the brief warns against.
- Bank-link initiation *does* depend on Redis for OAuth state — a Redis outage
  will legitimately fail that one operation, which is correct (there's nowhere
  else to safely put short-lived OAuth state mid-flow), not a violation of this
  ADR.

## Consequences
- Cache unavailability degrades performance (cache misses hit PostgreSQL
  directly) but never correctness or availability of the core read/write paths.
- We accept that "no Redis" during a bank-link initiation will surface as a
  user-visible failure for that specific operation; this is scoped and
  intentional, not an oversight.

## Alternatives considered
- **Redis as a secondary source of truth with write-through** — rejected: adds
  a second consistency model to reason about for no stated business need.
