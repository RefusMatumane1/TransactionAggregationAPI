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
| Dashboards/alerting configured in a real environment | ❌ Not verified — Grafana/Prometheus exist in `monitoring/`, but alert rules and on-call routing aren't confirmed | Must-have before launch |

## Testing

| Item | Status | Priority |
|---|---|---|
| Unit tests (domain, categorization, handlers) | ✅ Done, extensive | Must-have |
| Integration tests (API, EF Core, inbox/outbox) | ✅ Done (in-memory provider, not Testcontainers) | Must-have |
| Architecture tests (dependency boundaries) | ✅ Done, substantive | Should-have |
| Contract tests | ✅ Done (this review) | Should-have |
| Security tests (IDOR/BOLA) | ✅ Done (this review) | Must-have |
| Performance tests | ✅ Script + targets exist (this review), not yet run for real | Should-have |
| Integration tests against real Postgres/Redis (Testcontainers), not in-memory | ⚠️ Manually verified once against a real `postgres:16-alpine`/`redis:7-alpine` container (migration + stale-claim reclaim both confirmed working — see [failure-scenarios.md](failure-scenarios.md) scenarios 10/11/13); still no *automated, repeatable* coverage for the Postgres-specific claim SQL | Must-have — automate what was manually verified before relying on it long-term |

## Deployment

| Item | Status | Priority |
|---|---|---|
| Multi-stage, non-root, minimal Docker images | ✅ Done | Must-have |
| Rolling deployment with zero-downtime config | ✅ Done (`maxUnavailable: 0`) | Must-have |
| Migrations applied via a dedicated step, not every pod at boot | ✅ Fixed (this review) — see [ADR](adr/README.md) and the `Program.cs` `IsDevelopment()` gate | Must-have |
| CI/CD pipeline: build, format, static/dependency/container scan, SBOM | ✅ Done | Must-have |
| Automated deployment to staging/production | ❌ Deploy job in `ci-cd.yml` is a documented stub pending a `KUBECONFIG_DATA` secret | Must-have before relying on CI/CD for real deploys |
| Rollback strategy | ⚠️ Implied by Kubernetes rolling-update semantics; not documented as an explicit runbook | Should-have |

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
| Runbooks for common incidents (provider outage, DB failover, poison messages) | ❌ Not written — the threat model covers detection/mitigation conceptually, not step-by-step operator actions | Should-have before on-call rotation begins |
| On-call rotation / paging integration | ❌ Not evaluated in this repo | Must-have before real production traffic |

---

**Bottom line**: the application code and its CI/CD hardening are in strong
shape. The must-have gaps that remain are almost entirely *operational and
organizational*, not code: a real disaster-recovery test, a completed
deployment pipeline (the `KUBECONFIG_DATA`-gated deploy job), a compliance
review of data retention, and a third-party security assessment. None of
these can be satisfied by writing more code alone — they need explicit
decisions and follow-through from the team running this in production.
