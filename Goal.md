# Transaction Aggregation Platform — Engineering Goal

## 1\. Purpose

Build a **production-grade Transaction Aggregation Platform** for a banking environment using **.NET 10**.

The system receives financial transaction events from multiple external providers, normalizes and categorizes those transactions, persists them safely, and exposes a secure API for querying transaction and aggregation data.

The system must be designed and implemented at a level that demonstrates **strong Senior/SE3 engineering judgment**, with architectural decisions approaching a **Principal Engineer quality bar**.

This is not a CRUD demonstration.

The goal is to demonstrate that the system can remain:

- Correct under retries and duplicate delivery.
- Secure under hostile input and unauthorized access attempts.
- Resilient when dependencies fail.
- Observable when something goes wrong.
- Maintainable as the system evolves.
- Operationally simple.
- Horizontally scalable where appropriate.
- Testable at every important boundary.
- Safe to deploy repeatedly.

---

# 2\. Primary Engineering Principle

> **Production-grade does not mean maximum complexity.**

Every technology, abstraction, pattern, database structure, and infrastructure component must solve a real problem.

Prefer:

- Simple over clever.
- Explicit over magical.
- Boring over fragile.
- Strong invariants over application assumptions.
- Database-enforced correctness over timing assumptions.
- Observable behavior over implicit behavior.
- Automated verification over documentation claims.

Do not introduce:

- Microservices without a demonstrated need.
- CQRS without a meaningful read/write separation.
- Saga without a distributed compensating workflow.
- Redis without a meaningful caching/coordination requirement.
- Partitioning without sufficient data volume or query justification.
- Abstractions that merely wrap existing framework capabilities.

When a simpler solution is sufficient, choose it and document why.

---

# 3\. Target Architecture

The system shall be a **Modular Monolith**.

The architecture must provide strong module boundaries while retaining a single deployable application.

The design should allow a module to be extracted into a separate service in the future if there is a genuine scaling, ownership, or deployment reason.

The preferred architectural direction is:

```
External Providers
        |
        | Transaction Events
        v
+------------------------+
| Event Ingestion        |
| Authentication         |
| Validation             |
| Idempotency / Inbox    |
+-----------+------------+
            |
            v
+------------------------+
| Provider Adapters      |
| Canonicalization       |
+-----------+------------+
            |
            v
+------------------------+
| Transaction Domain     |
| Categorization         |
| Business Rules         |
+-----------+------------+
            |
            v
+------------------------+
| PostgreSQL             |
| Source of Truth        |
+-----------+------------+
            |
            v
+------------------------+
| Outbox                 |
| Integration Events     |
+------------------------+
            |
            v
     Future Consumers

REST API
   |
   v
Application Layer
   |
   v
Domain

Redis
   |
   +-- Cache / supporting infrastructure
   |
   +-- Never the source of truth for financial transactions
```

This diagram represents the intended direction, not a requirement to implement every component without justification.

---

# 4\. Core Business Capability

The platform must support the complete transaction lifecycle:

```
Provider Event
    ↓
Authenticate
    ↓
Validate
    ↓
Deduplicate
    ↓
Normalize
    ↓
Categorize
    ↓
Persist
    ↓
Publish reliable integration event
    ↓
Expose through API
    ↓
Aggregate / Query
```

The system must correctly handle:

- Multiple providers.
- Different provider schemas.
- Provider-specific identifiers.
- Different currencies.
- Provider timestamps.
- Processing timestamps.
- Duplicate events.
- Concurrent delivery.
- Delayed events.
- Out-of-order events.
- Malformed events.
- Retry.
- Provider failures.
- Infrastructure failures.
- Application restarts.

---

# 5\. Financial Data Correctness

Financial data must never be treated as ordinary CRU data. No Delete logic

The system must enforce strong correctness guarantees around:

- Money.
- Currency.
- Transaction identity.
- Account ownership.
- Transaction state.
- Provider identifiers.
- Event identity.
- Duplicate processing.
- Concurrent processing.

Never use floating-point types for monetary values.

Use exact decimal representation with explicit currency handling.

Important invariants must be enforced through both:

1. Application/domain logic.
2. Database constraints where appropriate.

The database must be treated as an important correctness boundary.

---

# 6\. Event Processing Guarantee

Assume event delivery is **at-least-once** unless a stronger guarantee is explicitly justified.

Therefore duplicate delivery is expected, not exceptional.

The architecture must guarantee that replaying the same event does not create duplicate financial transactions or duplicate business effects.

Idempotency must be enforced using stable identifiers and persistence-level guarantees.

The implementation must address the failure window:

```
Receive Event
    ↓
Process
    ↓
Commit Database
    ↓
Application crashes
    ↓
Broker never receives acknowledgement
    ↓
Event delivered again
```

The second delivery must not corrupt state or create a duplicate transaction.

This scenario must be reproduced by an automated test.

---

# 7\. Inbox and Outbox

Use the **Inbox Pattern** when required to make event consumption idempotent and recoverable.

Use the **Outbox Pattern** when required to guarantee reliable publication of events following a database transaction.

Do not implement either pattern merely because it is considered an enterprise pattern.

For each pattern, document:

- The failure it solves.
- The transaction boundary.
- Duplicate behavior.
- Retry behavior.
- Cleanup/retention.
- Concurrency behavior.
- Operational implications.

---

# 8\. Provider Isolation

Provider-specific concerns must remain isolated from the core domain.

A provider should be able to change:

- Event format.
- Field names.
- External identifiers.
- Transport details.
- Authentication mechanism.
- Provider-specific semantics.

without requiring changes throughout the transaction domain.

Use adapters or strategies where appropriate.

The core domain must operate on a canonical transaction model.

---

# 9\. Transaction Categorization

Transaction categorization must be deterministic and independently testable.

The categorization design must allow new rules/categories to be added without modifying unrelated business logic.

Potential patterns include:

- Strategy.
- Specification.
- Chain of Responsibility.

Use whichever design is most appropriate.

Do not introduce a pattern simply to demonstrate knowledge of design patterns.

---

# 10\. API

Expose a secure REST API for transaction retrieval and aggregation.

The API must support appropriate:

- Authentication.
- Authorization.
- Resource ownership checks.
- Validation.
- Filtering.
- Sorting.
- Pagination.
- Aggregation.
- Consistent error responses.
- API documentation.
- Versioning strategy.

Do not expose persistence entities directly.

Collection endpoints must be paginated.

Pagination must be designed against actual query patterns and database indexes.

Where large or frequently changing datasets make offset pagination problematic, evaluate keyset/cursor pagination.

---

# 11\. Security Goal

Treat the application as a banking system.

Security is a fundamental correctness requirement.

The system must provide:

- Authentication.
- Authorization.
- Resource-level access control.
- Least privilege.
- Input validation.
- Parameterized database access.
- Secure configuration.
- Secret externalization.
- Safe error responses.
- PII minimization.
- Sensitive-data protection.
- Rate limiting where appropriate.
- Security headers where appropriate.
- Dependency vulnerability management.
- Container security.

The following are absolute requirements:

```
No secrets in source.
No SQL injection.
No command injection.
No PII/secrets in logs.
No sensitive data in error responses.
No stack traces exposed to API consumers.
No IDOR/BOLA vulnerabilities.
```

Missing critical configuration must fail fast.

Secrets must come from environment/secret-management infrastructure rather than source code.

---

# 12\. Resilience Goal

Every external dependency must have deliberate failure behavior.

This includes:

- PostgreSQL.
- Redis.
- Message broker.
- External provider APIs.
- Event publishing.
- Event consumption.
- Other network dependencies.

For every integration point, explicitly determine:

- Timeout.
- Cancellation.
- Retryability.
- Maximum retries.
- Backoff.
- Jitter.
- Circuit breaking where appropriate.
- Permanent failure behavior.
- Dead-letter behavior where applicable.
- Logging.
- Metrics.
- Recovery.

Transient failures must not be treated the same as permanent/poison failures.

Never create infinite retry loops.

---

# 13\. Database Goal

Use **PostgreSQL** as the authoritative source of transaction data.

The database design must consider:

- Normalization.
- Referential integrity.
- Unique constraints.
- Foreign keys.
- Check constraints.
- Nullability.
- Exact monetary types.
- Timezone-safe timestamps.
- Appropriate schemas.
- Indexes.
- Composite indexes.
- Partial indexes where justified.
- Query plans.
- Concurrency.
- Transaction boundaries.
- Retention.

Every important index must correspond to an actual query shape.

Do not create indexes indiscriminately.

Evaluate partitioning based on realistic data volume and access patterns rather than implementing it prematurely.

---

# 14\. Database Evolution

Database schema changes must be versioned.

Use proper EF Core/PostgreSQL migrations.

Production must never rely on:

```
CREATE TABLE IF NOT EXISTS
```

as the migration strategy.

Migration deployment must distinguish between:

- Local development.
- Automated tests.
- Staging.
- Production.

Prefer safe expand/contract migrations for changes that require compatibility across application versions.

Do not blindly execute destructive migrations during application startup in production.

---

# 15\. Redis

Redis may be used for supporting concerns such as:

- Caching.
- Rate limiting.
- Frequently accessed reference data.
- Other explicitly justified use cases.

Redis must never become the authoritative source for financial transaction state.

The system should remain correct when Redis is unavailable unless Redis is explicitly part of a carefully justified consistency mechanism.

Cache behavior must define:

- TTL.
- Key structure.
- Invalidation.
- Failure behavior.
- Serialization.
- Memory considerations.

---

# 16\. Observability

Every important operation must be observable.

Use structured logging and OpenTelemetry where appropriate.

The system should provide:

- Structured logs.
- Correlation IDs.
- Trace IDs.
- Metrics.
- Distributed traces.
- Health checks.

Important operations include:

- Event ingestion.
- Event validation.
- Event deduplication.
- Transaction processing.
- Categorization.
- Database operations.
- External provider calls.
- Event publishing.
- API requests.
- Failures.
- Retries.
- Dead-letter processing.

Do not log unnecessary PII or sensitive financial information.

There must be no important execution path that becomes completely invisible when it fails.

---

# 17\. Health and Lifecycle

Provide appropriate:

```
/liveness
/readiness
/health
```

Health semantics must be deliberate.

Liveness should represent process health.

Readiness should represent whether the application is capable of serving traffic.

Optional dependencies must not unnecessarily make the application appear dead.

The application must:

- Start cleanly.
- Validate critical configuration.
- Wait for required dependencies appropriately.
- Stop gracefully.
- Drain work.
- Release resources correctly.
- Reuse connections appropriately.

---

# 18\. Performance

Performance must be measured rather than assumed.

The system must avoid:

- N+1 queries.
- Full-table scans on hot paths.
- Unbounded in-memory collections.
- Per-request connection creation.
- Loading unnecessarily large datasets.
- Unbounded event processing.
- Infinite retries.

Evaluate:

- Database query performance.
- Index effectiveness.
- API latency.
- Event processing latency.
- Throughput.
- Connection pooling.
- Batch processing.
- Caching.
- Concurrency.
- Backpressure.

Use realistic performance tests where appropriate.

Potential tools include:

- k6.
- NBomber.
- BenchmarkDotNet.

Performance optimizations must be evidence-driven.

---

# 19\. Testing Goal

The system must be verified, not merely compiled.

The test strategy must include:

### Unit Tests

For:

- Domain rules.
- Categorization.
- Validation.
- Result behavior.
- Business invariants.

### Integration Tests

For:

- PostgreSQL.
- EF Core.
- Database constraints.
- Transactions.
- Inbox.
- Outbox.
- Redis where applicable.
- Messaging where applicable.
- External provider adapters.

Prefer real infrastructure through containers/Testcontainers where practical.

### API Tests

For:

- Authentication.
- Authorization.
- BOLA/IDOR.
- Validation.
- Pagination.
- Filtering.
- Error responses.
- Problem Details.

### Concurrency Tests

Test:

- Concurrent duplicate events.
- Concurrent updates.
- Race conditions.
- Database constraints.

### Failure Tests

Test:

- Provider timeout.
- Provider 429.
- Provider 500.
- Database unavailable.
- Redis unavailable.
- Broker unavailable.
- Malformed event.
- Poison message.
- Application restart.
- Crash after database commit.
- Redelivery.

### Security Tests

Test:

- Injection.
- Unauthorized access.
- Resource ownership.
- Secret exposure.
- Unsafe error responses.

### Architecture Tests

Verify module dependency boundaries.

---

# 20\. Production Infrastructure

Use:

- .NET 10.
- Docker.
- .NET Aspire.
- Docker Compose.
- CI/CD.

Local development should be simple.

The local environment should provide only the infrastructure actually required.

Containers must:

- Use multi-stage builds.
- Use minimal runtime images.
- Pin base images appropriately.
- Run as a non-root user.
- Externalize configuration.
- Never contain secrets.
- Have appropriate healthchecks.
- Shut down gracefully.

Aspire should primarily improve local orchestration and developer experience rather than become an unnecessary runtime dependency.

---

# 21\. CI/CD Goal

Every change should pass automated verification before deployment.

The pipeline should include, where applicable:

```
Build
  ↓
Formatting / Static Analysis
  ↓
Unit Tests
  ↓
Integration Tests
  ↓
Architecture Tests
  ↓
Security Scanning
  ↓
Dependency Scanning
  ↓
Container Build
  ↓
Container Scan
  ↓
SBOM
  ↓
Artifact Publication
  ↓
Deployment
  ↓
Smoke Tests
```

Production deployment must use reproducible artifacts.

Configuration and secrets must be injected externally.

Database migrations must have a controlled deployment strategy.

---

# 22\. Security Verification

Security claims must be backed by executable evidence.

Use appropriate tools such as:

- OWASP ZAP.
- Trivy.
- Dependency vulnerability scanning.
- Secret scanning.
- Static analysis.
- SBOM generation.

Do not simply state:

> "The application is secure."

Instead demonstrate:

```
Threat
    ↓
Control
    ↓
Implementation
    ↓
Automated Test / Scan
    ↓
Result
```

---

# 23\. Documentation Goal

Documentation must explain **why**, not simply **what**.

Provide:

```
/docs
    architecture.md
    decisions/
    events/
    api.md
    database.md
    security.md
    resilience.md
    operations.md
    testing.md
    deployment.md
    requirements-traceability.md
    failure-mode-matrix.md
    production-readiness.md
    assessment/
```

Documentation should contain:

- Architecture diagrams.
- Module boundaries.
- Event flows.
- Database design.
- API examples.
- Security model.
- Failure modes.
- Operational procedures.
- Deployment process.
- Architectural decisions.
- Trade-offs.

Known limitations must describe the **actual failure mode and consequence**, not generic statements such as:

> "This could be improved in the future."

---

# 24\. Architecture Decision Records

Important decisions must have ADRs.

At minimum evaluate:

- Modular monolith vs microservices.
- Event transport.
- Inbox.
- Outbox.
- CQRS.
- Redis.
- PostgreSQL schema design.
- Partitioning.
- Pagination.
- Migration strategy.
- Saga.
- Provider adapter strategy.
- Authentication/authorization.
- Observability.

An ADR must contain:

```
Context
Decision
Alternatives
Reasoning
Trade-offs
Consequences
```

If a technology is intentionally rejected, document why.

---

# 25\. Failure-Mode Thinking

For every architectural guarantee, identify the failure it creates or permits.

Use:

```
Failure
    ↓
Detection
    ↓
Guard
    ↓
Recovery
    ↓
Consistency Result
    ↓
Observability
    ↓
Automated Test
```

At minimum evaluate:

- Duplicate delivery.
- Concurrent delivery.
- Out-of-order delivery.
- Delayed delivery.
- Application crash.
- Database failure.
- Broker failure.
- Provider failure.
- Redis failure.
- Deployment failure.
- Migration failure.
- Poison messages.
- Malformed input.
- Unauthorized access.
- Excessive request volume.

---

# 26\. Requirements Traceability

Create a requirements traceability matrix.

Every requirement must map to:

```
Requirement
    ↓
Implementation
    ↓
Test
    ↓
Executed Result
```

Example:

| Requirement                          | Implementation             | Test                    | Result |
| ------------------------------------ | -------------------------- | ----------------------- | ------ |
| Duplicate events are safe            | Inbox + unique constraint  | Redelivery test         | PASS   |
| Money uses exact precision           | PostgreSQL numeric mapping | Monetary precision test | PASS   |
| Unauthorized account access rejected | Resource authorization     | BOLA test               | PASS   |
| API collections paginated            | Cursor pagination          | API integration test    | PASS   |

No requirement may be considered complete merely because code exists.

---

# 27\. Evidence-Driven Engineering

A README claim is not proof.

For important behavior, provide concrete evidence:

- Source location.
- Test location.
- Command executed.
- Test output.
- Scan result.
- Container inspection.
- Database migration execution.
- Failure reproduction.

For example:

```
Claim:
Container runs as non-root.

Evidence:
Dockerfile:USER app
Command:
docker inspect ...
Result:
UID 10001
```

Confidence must be reduced when behavior has not been executed.

---

# 28\. Principal-Level Quality Bar

The final system should demonstrate the following engineering qualities:

### Correctness

The system remains correct under:

- Retry.
- Redelivery.
- Concurrency.
- Restart.
- Partial failure.
- Invalid input.

### Security

Security boundaries are explicit and tested.

### Reliability

External failures are expected and handled deliberately.

### Maintainability

The architecture is understandable to engineers who did not build it.

### Scalability

The system can scale horizontally where appropriate without shared in-memory business state.

### Observability

Production failures can be diagnosed without attaching a debugger.

### Operational Simplicity

The system does not contain infrastructure that does not provide meaningful value.

### Evolvability

Providers, APIs, events and database schemas can evolve safely.

---

# 29\. Definition of Done

The project is not considered production-grade until:

- Every stated requirement is implemented.
- Every requirement has executable test evidence.
- Duplicate/redelivery behavior is verified.
- Concurrency behavior is verified.
- Failure paths are tested.
- Database migrations are verified.
- API validation is verified.
- Authorization is verified.
- No secrets exist in source.
- No injection vulnerabilities exist.
- No PII/secrets appear in logs.
- No stack traces leak to clients.
- All external integrations have deliberate error handling.
- Transient and permanent failures are distinguished.
- Retry behavior is bounded.
- Poison messages cannot cause infinite retry loops.
- Collections are paginated.
- Query shapes have appropriate indexes.
- No N+1 queries exist on important paths.
- Unit tests pass.
- Integration tests pass.
- Security scans pass.
- Container scans pass.
- Containers run as non-root.
- Health/readiness behavior is verified.
- Graceful shutdown is verified.
- Structured logs and correlation IDs exist on important paths.
- Documentation explains important architectural decisions.
- Architecture diagrams exist.
- Failure modes are documented.
- The application builds from a clean environment.
- The application boots successfully.
- The documented scenarios can actually be reproduced.

---

# 30\. Self-Verification Requirement

Before declaring the project complete, perform a full self-assessment.

Evaluate every category:

1. Requirements Coverage.
2. Problem Interpretation & Solution Fit.
3. Architecture & Scalability.
4. Resilience & Operational Readiness.
5. Data & Domain Modelling.
6. API Design & Contracts.
7. Security.
8. Code Quality & Maintainability.
9. Testing.
10. Observability.
11. Event-Driven Design.
12. Infrastructure & DevOps.
13. Documentation & Communication.

Use the project assessment rubric supplied with this repository.

For each category record:

```
Score
Evidence
Confidence
Remaining Gap
Remediation
```

Do not award yourself credit based solely on documentation.

If something has not been executed, mark it accordingly.

---

# 31\. Final Success Criterion

The ultimate objective is not:

> "Build a complicated .NET application."

The objective is:

> **Build a small, credible banking-grade transaction platform whose correctness, security, resilience, architecture and operational behavior can be demonstrated with evidence.**

The strongest solution is the one that a Principal Engineer can review and say:

- The boundaries make sense.
- The failure modes were understood.
- The database protects correctness.
- Events are safely processed.
- Security is deliberate.
- Operations are observable.
- Tests prove the important guarantees.
- Infrastructure is appropriately sized.
- Complexity has been justified.
- The system can evolve without a rewrite.

  **Production-grade means the system can be trusted because its guarantees are designed, implemented, tested, and evidenced — not because the codebase contains many enterprise patterns.**
