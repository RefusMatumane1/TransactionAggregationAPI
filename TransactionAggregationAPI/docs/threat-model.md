# Threat Model — Transaction Aggregation API

STRIDE-based, lightweight. Scope: transaction ingestion, the public API, provider
integrations, PostgreSQL, Redis, authentication/authorization, and secrets. This
is not a claim of completeness or of "100% secure" — it records what has been
considered and mitigated, and what residual risk is knowingly accepted.

## 1. Event/transaction ingestion (webhook + bank-link sync)

| | |
|---|---|
| **Threat** | Spoofed webhook source pushes fabricated transactions into a customer's history. |
| **Impact** | Financial data integrity — fake income/expenses corrupt reporting and could mislead a customer. |
| **Likelihood** | Medium if webhook API keys leak; low otherwise. |
| **Mitigation** | Each `WebhookSource` has its own rotatable API key (`RotateWebhookSourceKeyCommandHandler`); sources can be deactivated instantly (`DeactivateWebhookSourceCommandHandler`). Ingested transactions are scoped to the `BankLink`/source that authenticated the request, not a client-supplied customer ID. |
| **Detection** | Structured logs on ingestion (`ProcessInboundTransactionsCommandHandler` logs source name + count); anomalous volume from a single source is visible in metrics/logs. |
| **Residual risk** | If a webhook API key leaks, the holder can push transactions until the key is rotated/the source is deactivated. Key rotation is manual today, not automatic on suspected compromise. |

| | |
|---|---|
| **Threat** | Duplicate/replayed event causes a transaction to be double-counted. |
| **Impact** | Incorrect balances/summaries. |
| **Likelihood** | Medium — at-least-once delivery is assumed, not exceptional. |
| **Mitigation** | Database-level unique constraint on `(CustomerId, Source.ExternalId)`; concurrent duplicate inserts are caught as Postgres error 23505 and resolved as a no-op success, not a crash (see ADR-0002). |
| **Detection** | The duplicate-catch path logs a warning (`"Duplicate transaction external id..."`). |
| **Residual risk** | None significant — this is enforced at the database, not just in application code, so it holds even under concurrent processing. |

## 2. Public API

| | |
|---|---|
| **Threat** | IDOR/BOLA — a caller edits an ID in the URL (e.g. `accountId`, `customerId`) to access another customer's data. |
| **Impact** | Cross-customer financial data disclosure — severe for a banking system. |
| **Likelihood** | High if unmitigated; this class of bug is the most common API vulnerability in practice. |
| **Mitigation** | Every account/customer-scoped endpoint checks `customerId != userContext.UserId` before acting (`AccountEndpoints.cs`), and `GetAccountById` additionally re-verifies the returned account's `CustomerId` matches the caller — closing the specific case where a valid account ID belongs to a *different* customer than the one in the URL. |
| **Detection** | A rejected object-level check should be logged/alertable (verify this is wired to a security-relevant log category, not just a generic 403). |
| **Residual risk** | Any endpoint added later that forgets this check reintroduces the vulnerability — this is a per-endpoint discipline, not a framework-enforced guarantee. Consider an architecture test that fails if a new endpoint touching customer-scoped data lacks an ownership check, as a future hardening step. |

| | |
|---|---|
| **Threat** | Credential stuffing / brute force against authentication. |
| **Impact** | Account takeover. |
| **Likelihood** | Medium (public internet-facing API). |
| **Mitigation** | Authentication is delegated to Keycloak (OIDC/JWT), not custom password handling. Rate limiting is applied per-user/IP (`RedisFixedWindowRateLimiter`, 100 req/min). |
| **Detection** | Keycloak's own auth logs; not yet correlated into this system's own structured logs. |
| **Residual risk** | Rate limiting is generic (all endpoints), not tuned specifically for auth-adjacent endpoints; brute-force lockout policy lives entirely in Keycloak configuration, outside this codebase. |

| | |
|---|---|
| **Threat** | Denial of service via oversized/abusive requests. |
| **Impact** | Availability. |
| **Likelihood** | Medium. |
| **Mitigation** | Global rate limiting fails open (not closed) on Redis failure by design (`AllowRequestOnRedisFailure = true` — a deliberate availability-over-strictness trade, see ADR-0005); HTTPS/HSTS enforced; response compression enabled. |
| **Residual risk** | No explicit request body size limit was confirmed in this review — verify Kestrel's default `MaxRequestBodySize` is intentionally sized for the largest legitimate payload (e.g. CSV export requests, webhook payloads), not left at the framework default by accident. |

## 3. Provider integrations (bank aggregator)

| | |
|---|---|
| **Threat** | Provider returns malformed/malicious data (e.g. absurd amounts, injection attempts in description fields). |
| **Impact** | Data integrity; potential stored-XSS if descriptions are ever rendered unescaped in the Blazor UI. |
| **Likelihood** | Low-medium — providers are semi-trusted but not infallible. |
| **Mitigation** | `Money.Create` validates amount/currency at construction; Blazor encodes output by default. |
| **Residual risk** | No explicit upper-bound sanity check on transaction amounts was confirmed (e.g. rejecting a $10-billion "grocery" transaction as implausible) — currently only currency/decimal validity is enforced, not business-plausibility. |

| | |
|---|---|
| **Threat** | Provider outage or slow responses cascade into API unavailability. |
| **Impact** | Availability of bank-link operations. |
| **Likelihood** | Medium — third-party dependency. |
| **Mitigation** | `AddStandardResilienceHandler()` applies retry/timeout/circuit-breaking to all outbound `HttpClient`s, including the bank aggregator client, centrally rather than per-call. |
| **Residual risk** | Standard resilience defaults may not be tuned per-provider (e.g. a provider with known higher latency might need a longer timeout than the default) — verify defaults are appropriate, not just present. |

## 4. Database (PostgreSQL)

| | |
|---|---|
| **Threat** | SQL injection. |
| **Impact** | Full data compromise. |
| **Likelihood** | Low — all queries go through EF Core's parameterized LINQ; no raw SQL string concatenation was found in the reviewed code. |
| **Mitigation** | EF Core parameterization by default. |
| **Residual risk** | Any future raw SQL (`FromSqlRaw`, migrations with string interpolation) would need explicit review — not currently present, but worth a standing rule. |

| | |
|---|---|
| **Threat** | Credential compromise (database connection string leak). |
| **Impact** | Full data compromise. |
| **Likelihood** | Low if secrets management is followed. |
| **Mitigation** | No connection strings/secrets are committed in `appsettings.json` (verified — placeholders only); `k8s/secrets.yaml` uses `CHANGE_ME_BEFORE_DEPLOY` placeholders with an explicit header warning against committing real secrets. |
| **Residual risk** | This depends on operational discipline at deploy time (someone must actually inject real secrets via a secret manager, not commit them) — the codebase can't enforce this by itself. |

## 5. Redis

| | |
|---|---|
| **Threat** | Redis compromise or outage. |
| **Impact** | Cache data (low sensitivity) and OAuth state disclosure/loss; **not** financial data (see ADR-0005 — Redis is never the source of truth). |
| **Likelihood** | Low-medium. |
| **Mitigation** | Application degrades gracefully on Redis failure (cache miss, rate-limit fail-open); readiness health check reports Degraded rather than Unhealthy on Redis failure (ADR-0005) so an outage doesn't cascade into API unavailability. |
| **Residual risk** | Bank-link initiation depends on Redis for OAuth state; a Redis outage during that narrow window legitimately fails that one operation — accepted and scoped, not a gap. |

## 6. Secrets and configuration

| | |
|---|---|
| **Threat** | Secret sprawl — secrets scattered across appsettings, Docker images, source control. |
| **Impact** | Credential compromise. |
| **Likelihood** | Low, given current practice. |
| **Mitigation** | Strongly-typed `Options` classes read from configuration (not hardcoded); `appsettings.Development.json` contains only an explicitly-labeled `dev-only-admin-client-secret`; production secrets are expected to come from a proper secret manager / k8s secrets, not the repo. Gitleaks now scans full git history (not just the current file state) on every push/PR (`.github/workflows/ci-cd.yml`), with `.gitleaks.toml` allowlisting the specific known-fake placeholder values by exact content — not by file path — so a genuinely new secret added anywhere, including `k8s/secrets.yaml`, is still caught. |
| **Residual risk** | This review did not verify which secret manager is actually wired up in the target production environment (Azure Key Vault, HashiCorp Vault, sealed-secrets, etc.) — `k8s/secrets.yaml`'s placeholders imply the operator is responsible for this at deploy time. Running gitleaks against full history surfaced one historical `jwt-secret` value (an old, since-removed field from before the project moved to Keycloak auth) with genuinely random-looking content rather than an obvious placeholder — confirmed with the repo owner as never used against a real deployment, and added to the allowlist. Git history is otherwise immutable in place; if it's ever unclear whether a historical value was real, treat it as compromised and rotate, since removing the current file's content does not remove it from history. |

## Not claimed
This threat model does not cover: physical security, insider threat, supply-chain
compromise of NuGet dependencies beyond Dependabot's automated scanning, or a full
penetration test. A weekly/manual OWASP ZAP baseline scan
(`.github/workflows/zap-baseline.yml`) now runs passive dynamic checks against the
API (missing security headers, verbose error disclosure, cookie flags) — this closes
part of section 21's "dynamic API security testing" gap, but a passive baseline scan
is not an active scan and is not a substitute for a real penetration test. This
threat model should be revisited whenever a new external integration, authentication
method, or data flow is added.
