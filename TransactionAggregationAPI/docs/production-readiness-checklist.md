# Production readiness checklist

Instructions.md section 44. Status reflects what's actually in the codebase as
of this checklist's last update, not aspiration. "Must-have" items block a
real production launch handling customer financial data; "should-have" items
are strongly recommended but wouldn't block a small/early launch on their own;
"future enhancement" items are explicitly not justified yet — see the linked
ADR for why.

## Security

| Item | Status | Priority |
|---|---|---|
| Authentication via OIDC (Keycloak), no custom password handling | ✅ Done | Must-have |
| Object-level authorization (IDOR/BOLA) on customer-scoped resources | ✅ Done, regression-tested (`AccountApiSecurityTests`) | Must-have |
| Rate limiting | ✅ Done (Redis-backed, fails open) | Must-have |
| Secrets never committed to source control | ✅ Verified — placeholders only | Must-have |
| Dependency vulnerability scanning in CI | ✅ Done (`dotnet list package --vulnerable`, Dependabot) | Must-have |
| Secret scanning across full git history (not just current file state) | ✅ Done (Gitleaks, `.gitleaks.toml`) — surfaced and resolved one historical placeholder that needed explicit confirmation it was never real; see the threat model section 6 | Must-have |
| Container image scanning | ✅ Done (Trivy, SARIF → Code Scanning) | Must-have |
| SBOM generation | ✅ Done | Should-have |
| Dynamic API security testing (DAST) | ✅ Passive baseline only (ZAP, manual/weekly) — not an active scan or a real pentest | Should-have |
| Third-party penetration test | ❌ Not done | Must-have before handling real customer financial data |
| Field-level encryption for PII beyond bank-link tokens | ❌ Not done — see [data-retention.md](data-retention.md) | Should-have, pending compliance review |

## Reliability

| Item | Status | Priority |
|---|---|---|
| Idempotent event processing (DB-level uniqueness) | ✅ Done | Must-have |
| Retry/timeout/circuit-breaker on outbound HTTP | ✅ Done (`AddStandardResilienceHandler`, centralized) | Must-have |
| Graceful degradation when Redis is unavailable | ✅ Done, health check no longer cascades | Must-have |
| Health checks split liveness vs. readiness | ✅ Done | Must-have |
| Disaster recovery / backup-restore runbook for Postgres | ❌ Not documented in this repo | Must-have |
| Documented failure-scenario responses (Instructions.md section 42) | ✅ Done ([failure-scenarios.md](failure-scenarios.md)) — walking through it surfaced and fixed a real stale-claim gap in the Inbox/Outbox dispatchers (see below) | Should-have |
| Inbox/Outbox stale-claim recovery (crash between claim and commit) | ✅ Fixed and manually verified against real PostgreSQL (this review) — reclaim after a configurable `ClaimTimeoutMinutes` | Must-have |

## Performance

| Item | Status | Priority |
|---|---|---|
| Indexed hot-path queries | ✅ Done, targeted composite indexes | Must-have |
| Deliberate pagination strategy with a documented revisit threshold | ✅ Done ([ADR-0007](adr/0007-offset-pagination.md)) | Must-have |
| Load-testing strategy with documented targets | ✅ Done (`perf/`), not yet run against a real deployment | Should-have |
| Actual load test executed against staging before launch | ❌ Not done | Must-have before a launch with meaningful expected traffic |

## Observability

| Item | Status | Priority |
|---|---|---|
| Structured logging, no sensitive-data leakage | ✅ Done (Serilog + `SensitiveDataDestructuringPolicy`) | Must-have |
| Distributed tracing, metrics (OpenTelemetry) | ✅ Done | Must-have |
| Trace ID on every error response | ✅ Done (this review) | Must-have |
| Dead-lettered (poison) message counters | ✅ Done (this review) — `inbox_messages_dead_lettered_total` / `outbox_messages_dead_lettered_total` (see [failure-scenarios.md](failure-scenarios.md) scenario 17) | Should-have |
| Dashboards/alerting configured in a real environment | ❌ Not verified — Grafana/Prometheus exist in `monitoring/`, but alert rules and on-call routing aren't confirmed. The dead-letter counters above give something to alert *on*; no rule is wired up yet | Must-have before launch |

## Testing

| Item | Status | Priority |
|---|---|---|
| Unit tests (domain, categorization, handlers) | ✅ Done, extensive | Must-have |
| Integration tests (API, EF Core, inbox/outbox) | ✅ Done (in-memory provider, not Testcontainers) | Must-have |
| Architecture tests (dependency boundaries) | ✅ Done, substantive | Should-have |
| Contract tests | ✅ Done (this review) | Should-have |
| Security tests (IDOR/BOLA) | ✅ Done (this review) | Must-have |
| Performance tests | ✅ Script + targets exist (this review), not yet run for real | Should-have |
| Integration tests against real Postgres (Testcontainers), not in-memory | ✅ Done (this review) — `TransactionAggregation.Tests/Integration/Postgres/` spins up a real `postgres:16-alpine` container per test class and runs actual EF Core migrations against it, permanently automating what was previously a manual, one-off `docker exec psql` check: migration application, the Inbox/Outbox stale-claim reclaim (`FOR UPDATE SKIP LOCKED`), and the exact concurrent-duplicate-insert race from Instructions.md section 6 against the real unique constraint (not the in-memory provider, which doesn't enforce it the same way and can't produce the Postgres-specific 23505 error the handler's catch clause checks for) | Must-have |

## Deployment

| Item | Status | Priority |
|---|---|---|
| Multi-stage, non-root, minimal Docker images | ✅ Done | Must-have |
| Container-level hardening (read-only root filesystem, dropped capabilities, no privilege escalation) | ✅ Done and verified live (this review) — `docker run --read-only --tmpfs /tmp --cap-drop=ALL --security-opt=no-new-privileges` starts cleanly; `/tmp` mounted as an `emptyDir` for .NET's temp-file needs | Must-have |
| Rolling deployment with zero-downtime config | ✅ Done (`maxUnavailable: 0`) | Must-have |
| Migrations applied via a dedicated step, not every pod at boot | ✅ Fixed (this review) — see [ADR](adr/README.md) and the `Program.cs` `IsDevelopment()` gate | Must-have |
| Migration Job actually runnable (not missing required config) | ✅ Fixed and verified live (this review) — `migration-job.yaml` was missing the Seq/Keycloak/Redis config `Program.cs` requires at startup; it would have crashed before ever applying a migration. See [failure-scenarios.md](failure-scenarios.md) scenario 14. | Must-have |
| Rollback strategy | ⚠️ Documented (this review, [operations.md](operations.md) §6) — and documenting it surfaced a real gap: `k8s/api/deployment.yaml` pins `image: transactionaggregationapi:latest`, so `kubectl rollout undo` cannot reliably restore previous code under this manifest as it stands. A working rollback today means manually re-pointing the Deployment at a known-good, explicitly-tagged image. Pinning to immutable versioned tags is the actual fix, not yet done. | Should-have |
| Immediate risk from the missing Helm chart: `configmap.yaml` defaulted every environment to Development | ✅ Fixed and verified live (this review) — `ASPNETCORE_ENVIRONMENT` now defaults to `Production` in the shared ConfigMap (the plain-`kubectl apply` path has no per-environment templating to override it otherwise). This re-enables every `IsDevelopment()`-gated protection for a real deployment: no demo-data seeding, no Swagger UI exposure, no detailed EF Core errors, no localhost CORS fallback. Fixing this required decoupling `Keycloak:RequireHttpsMetadata` from `IsDevelopment()` into its own explicit config value (`Program.cs`), since the in-cluster Keycloak service serves plain HTTP internally — flipping the environment alone would have broken every authenticated request. Verified: ran the actual image with `ASPNETCORE_ENVIRONMENT=Production`, a plain-HTTP Keycloak authority, and `Keycloak__RequireHttpsMetadata=false` against real Postgres/Redis — no exception, dispatchers started, no seeding/migration occurred. | Must-have |
| **Helm chart is real, not just scaffolding** | ❌ **Explicitly deferred as future work** (repo owner's decision) — `k8s/helm/Chart.yaml` and per-environment `values.dev/staging/production.yaml` exist, but there is still no `templates/` directory, so no Helm chart actually renders these values into manifests. The `helm.sh/hook` annotations on `migration-job.yaml` remain inert without a chart to interpret them. The immediate safety risk this created (above) is fixed; a real per-environment templating story is not, and multiple environments deployed via plain `kubectl apply` still share one static ConfigMap. | Should-have — revisit if/when more than one environment needs to diverge beyond what a single ConfigMap can express |
| CI/CD pipeline: build, format, static/dependency/container scan, SBOM | ✅ Done | Must-have |
| Automated deployment to staging/production | ❌ Deploy job in `ci-cd.yml` is a documented stub pending a `KUBECONFIG_DATA` secret | Must-have before relying on CI/CD for real deploys |

## Database

| Item | Status | Priority |
|---|---|---|
| Normalized schema with meaningful constraints/indexes | ✅ Done | Must-have |
| Partitioning evaluated (not blindly applied) | ✅ Done ([ADR-0006](adr/0006-no-partitioning-yet.md)) | Must-have |
| Connection pooling, retry-on-failure | ✅ Done (Npgsql `EnableRetryOnFailure`) | Must-have |
| Backup/restore tested | ❌ Not verified in this repo | Must-have |

## Data protection

| Item | Status | Priority |
|---|---|---|
| Encryption at rest for sensitive tokens | ✅ Done (`IBankLinkCredentialProtector`) | Must-have |
| Data retention policy confirmed with compliance/legal | ❌ Not done — see [data-retention.md](data-retention.md) for the open questions | Must-have before handling real customer financial data |
| Right-to-erasure capability | ❌ Does not exist (no customer-deletion endpoint at all) | Must-have if any applicable regulation requires it |

## Disaster recovery

| Item | Status | Priority |
|---|---|---|
| Documented RTO/RPO targets | ❌ Not defined | Must-have before production launch |
| Tested restore procedure | ❌ Not done | Must-have |
| Multi-AZ/region considerations | ❌ Not evaluated — likely premature at current scale, but should be an explicit decision, not silence | Future enhancement, revisit if/when uptime requirements demand it |

## Configuration

| Item | Status | Priority |
|---|---|---|
| Strongly-typed options, validated at startup | ✅ Done (Keycloak config fails fast if missing) | Must-have |
| Per-environment configuration (dev/staging/production) | ✅ Done (Helm values files) | Must-have |
| No scattered raw `IConfiguration` string-indexing in business logic | ✅ Verified | Should-have |

## Dependencies

| Item | Status | Priority |
|---|---|---|
| Dependency scanning + auto-update PRs | ✅ Done (Dependabot) | Must-have |
| Minimal, justified dependency footprint | ✅ Verified — no unjustified packages found in the audit | Should-have |

## Documentation

| Item | Status | Priority |
|---|---|---|
| ADRs for major decisions | ✅ Done | Should-have |
| Threat model | ✅ Done | Should-have |
| Architecture diagrams (context, container, event flow, ERD, deployment) | ✅ Done (this review) | Should-have |
| Data retention assumptions | ✅ Documented as open questions (this review) | Should-have |
| This checklist | ✅ Done | Should-have |

## Operations

| Item | Status | Priority |
|---|---|---|
| Runbooks for common incidents (provider outage, DB failover, poison messages) | ✅ Done (this review, [operations.md](operations.md)) — provider outage, DB/Redis failure, poison-message pileup, migration Job failure, rollback, and unauthorized-access response, with real `kubectl`/`psql` commands grounded in the actual manifests, not conceptual descriptions. Writing it surfaced two real gaps, fixed/documented in place: the `:latest` image tag breaking `kubectl rollout undo` (see the Rollback strategy row above), and bank-link token encryption being silently unrecoverable if protected during a Redis-outage ephemeral-key fallback | Should-have |
| On-call rotation / paging integration | ❌ Not evaluated in this repo | Must-have before real production traffic |

---

**Bottom line**: the application code and its CI/CD hardening are in strong
shape. The must-have gaps that remain are almost entirely *operational and
organizational*, not code: a real disaster-recovery test, a completed
deployment pipeline (the `KUBECONFIG_DATA`-gated deploy job), a compliance
review of data retention, and a third-party security assessment. None of
these can be satisfied by writing more code alone — they need explicit
decisions and follow-through from the team running this in production.
