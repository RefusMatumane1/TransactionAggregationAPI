You are acting as a **Principal Software Engineer / Staff+ .NET Architect** designing and implementing a production-grade financial transaction aggregation platform for a regulated banking environment.

Your job is NOT to generate a toy CRUD application.

Design the system as if it will eventually process real financial transaction events, operate continuously in production, handle failures and security threats, and be maintained by multiple engineering teams over several years.

The implementation target is:

- .NET 10
- C#
- ASP.NET Core
- PostgreSQL
- Redis
- .NET Aspire
- Docker
- Docker Compose
- CI/CD
- Event-driven transaction ingestion
- Modular Monolith
- Clean Architecture / Hexagonal Architecture
- SOLID
- Strong automated testing
- Production-grade observability
- High security
- Low operational and maintenance complexity

Do not introduce complexity merely to make the architecture look "enterprise". Every architectural decision must have a clear reason, and you must explicitly identify where simpler approaches are preferable.

---

# 1\. PRIMARY BUSINESS CAPABILITY

Build a **Transaction Aggregation API**.

Transactions arrive through events from multiple financial providers.

The system must:

1. Receive transaction events.
2. Validate and authenticate incoming events.
3. Deduplicate events safely.
4. Normalize provider-specific transaction formats.
5. Categorize transactions.
6. Persist normalized transactions.
7. Maintain customer/account relationships.
8. Expose transactions through a REST API.
9. Support filtering, sorting and pagination.
10. Provide aggregation/reporting endpoints.
11. Handle duplicate, delayed, reordered and failed events.
12. Remain resilient when external providers or infrastructure components fail.
13. Provide strong observability and auditability.

Assume providers can have different event schemas, identifiers, currencies, timestamps and transaction semantics.

The domain model must NOT become coupled to provider-specific contracts.

---

# 2\. ARCHITECTURAL STYLE

Use a **Modular Monolith**.

Do NOT start with microservices.

The system should have strong module boundaries and be designed so that a module can be extracted into a service later if there is a genuine business or scaling reason.

Use:

- Clean Architecture
- Hexagonal Architecture / Ports and Adapters
- Domain-driven boundaries where appropriate
- Dependency Inversion
- SOLID
- Explicit module boundaries
- CQRS where it provides real value
- Domain events where appropriate

Avoid:

- Anemic "service/repository/controller" architecture everywhere.
- Generic repositories without a clear reason.
- Shared mutable state between modules.
- Domain logic inside controllers.
- Infrastructure dependencies leaking into the domain.
- Provider-specific models leaking into the domain.
- Premature microservices.

---

# 3\. MODULE DESIGN

Propose and justify module boundaries.

A starting point may be:

- Transactions
- Transaction Ingestion
- Categorization
- Providers
- Customers / Accounts
- Reporting / Analytics
- Audit

But do NOT blindly follow this list.

Determine the appropriate boundaries based on cohesion, coupling, ownership and change patterns.

Each module should have clear:

- Domain
- Application
- Infrastructure
- Contracts

boundaries where appropriate.

Explain dependencies between modules.

Prefer:

```
API
 ↓
Application
 ↓
Domain
 ↓
Ports
 ↓
Infrastructure Adapters
```

rather than allowing arbitrary dependencies.

---

# 4\. EVENT-DRIVEN INGESTION

Transactions arrive as events.

Design an event ingestion pipeline such as:

```
Provider
   ↓
Event
   ↓
Ingress / Consumer
   ↓
Authentication
   ↓
Schema Validation
   ↓
Idempotency / Inbox
   ↓
Provider Adapter
   ↓
Canonical Transaction
   ↓
Domain Processing
   ↓
Persistence
   ↓
Outbox
   ↓
Integration Events
```

Consider:

- At-least-once delivery.
- Duplicate events.
- Out-of-order events.
- Delayed events.
- Poison messages.
- Retry behavior.
- Dead-letter handling.
- Event versioning.
- Schema evolution.
- Correlation IDs.
- Causation IDs.
- Event IDs.
- Provider transaction IDs.
- Idempotency keys.

Explain whether Kafka, RabbitMQ, Azure Service Bus, or another broker is appropriate for this project.

For the local development environment, keep the infrastructure manageable.

Do not introduce a message broker solely for architectural theatre.

If a broker is required, make the local developer experience simple through Aspire/Docker.

---

# 5\. INBOX / OUTBOX

Evaluate and implement the **Inbox Pattern** where required for idempotent event consumption.

Evaluate and implement the **Outbox Pattern** where required for reliable publication of integration events.

Explain:

- Why Inbox is needed.
- Why Outbox is needed.
- Transaction boundaries.
- Failure scenarios.
- Duplicate handling.
- Retry behavior.
- Cleanup/retention.
- Indexing strategy.
- Concurrency handling.

Avoid implementing both patterns everywhere without justification.

---

# 6\. IDEMPOTENCY

Idempotency is mandatory.

The system must remain correct when the same transaction event is delivered multiple times.

Design database-level guarantees rather than relying solely on application-level checks.

Consider:

- Event ID uniqueness.
- Provider + external transaction ID uniqueness.
- Account scope.
- Idempotency keys.
- Concurrent processing.
- Retry after partial failure.

Explain race conditions such as:

```
Request A → check transaction → not found
Request B → check transaction → not found
Request A → insert
Request B → insert
```

and demonstrate how the architecture/database prevents duplicate data.

---

# 7\. DOMAIN MODEL

Design a financially appropriate domain model.

Consider:

- Customer
- Account
- Transaction
- TransactionCategory
- Provider
- ProviderAccount
- TransactionEvent
- AggregationRun
- InboxMessage
- OutboxMessage
- AuditEntry

Do not create entities simply because they sound enterprise.

Explain which concepts are entities, value objects, aggregates or simple persistence models.

Money must be represented safely.

Do not use floating-point types for monetary values.

Consider:

- Decimal precision.
- Currency.
- Debit/credit semantics.
- Transaction status.
- Transaction timestamps.
- Provider timestamps vs processing timestamps.
- Time zones.
- Reversals.
- Corrections.

---

# 8\. TRANSACTION CATEGORIZATION

Implement a rule-based categorization system.

Use appropriate design patterns where they genuinely help, for example:

- Strategy
- Specification
- Chain of Responsibility

Potential categories:

- Groceries
- Transport
- Utilities
- Entertainment
- Shopping
- Salary
- Transfer
- Banking
- Other

The categorization system must be:

- Testable.
- Extensible.
- Deterministic.
- Independent of HTTP.
- Independent of EF Core.
- Independent of provider-specific APIs.

Adding a new categorization rule should not require modifying unrelated code.

---

# 9\. RESULT PATTERN

Use a consistent Result pattern where it improves domain/application error handling.

Distinguish between:

- Expected business failures.
- Validation failures.
- Not found.
- Conflict.
- Unauthorized/forbidden.
- External provider failures.
- Infrastructure failures.
- Unexpected programming errors.

Do not use exceptions as normal control flow.

Do not overuse Result\<T\> where normal return values are sufficient.

Explain where Result is appropriate and where exceptions remain appropriate.

---

# 10\. GLOBAL EXCEPTION HANDLING

Implement centralized exception handling.

Use:

- ASP.NET Core exception handling middleware / handler.
- Problem Details.
- Consistent error contracts.
- Correlation/trace IDs.
- Safe error messages.

Never expose:

- Stack traces.
- Database details.
- Connection strings.
- Secrets.
- Internal implementation details.

Production errors should be useful to operators without leaking sensitive information to clients.

---

# 11\. API DESIGN

Build a professional REST API.

Include appropriate endpoints for:

- Customer transactions.
- Individual transactions.
- Filtering.
- Date ranges.
- Categories.
- Transaction types.
- Accounts.
- Aggregated summaries.
- Spending by category.
- Monthly summaries.
- Synchronization/ingestion status where applicable.

Use:

- DTOs.
- Explicit API contracts.
- API versioning strategy.
- Problem Details.
- Validation.
- Correct HTTP status codes.
- Pagination.
- Sorting.
- Filtering.

Do not expose EF Core entities directly.

---

# 12\. PAGINATION

Design pagination deliberately.

Compare:

- Offset pagination.
- Keyset/cursor pagination.

Determine which is appropriate for:

- Transaction history.
- Large datasets.
- Frequently changing data.

Consider indexes required to support the chosen pagination strategy.

Avoid queries that become increasingly expensive as page numbers increase.

---

# 13\. POSTGRESQL DATABASE DESIGN

Design PostgreSQL as a production database.

Consider:

- Normalization.
- Appropriate denormalization where justified.
- Primary keys.
- Foreign keys.
- Unique constraints.
- Check constraints.
- Indexes.
- Composite indexes.
- Partial indexes.
- Covering indexes where appropriate.
- Query plans.
- Connection pooling.
- Concurrency.
- Transaction isolation.
- Soft deletion only where genuinely required.
- Auditability.

Explain every important index.

Do not blindly index every column.

---

# 14\. DATABASE PARTITIONING

Evaluate whether transaction tables require partitioning.

Consider partitioning by:

- Transaction date.
- Account/customer.
- Another appropriate key.

Do NOT automatically partition the database.

Explain:

- Expected volume.
- Query patterns.
- Retention requirements.
- Partition maintenance.
- Index implications.
- Operational complexity.

If partitioning is not justified at the initial scale, document the threshold/conditions under which it should be introduced.

---

# 15\. DATABASE SCHEMA

Use PostgreSQL schemas where they provide meaningful isolation.

Evaluate whether modules should have separate database schemas such as:

```
transactions.*
providers.*
audit.*
integration.*
```

Avoid pretending that PostgreSQL schemas provide security boundaries if the actual deployment does not enforce them.

Document the reasoning.

---

# 16\. EF CORE

Use EF Core appropriately.

Consider:

- Fluent configuration.
- Separate entity configurations.
- Migrations.
- Optimized queries.
- AsNoTracking where appropriate.
- Compiled queries only if justified.
- Transaction boundaries.
- Concurrency handling.
- Value conversions.
- PostgreSQL-specific features where valuable.

Avoid:

- N+1 queries.
- Accidental eager loading.
- Generic repository abstractions that hide EF Core's capabilities.
- Loading huge datasets into memory.
- Tracking entities unnecessarily.

---

# 17\. MIGRATIONS

Database migrations must be production-safe.

Design a proper migration strategy.

Consider:

- Forward-only migrations.
- Startup migration vs deployment migration.
- CI validation.
- Migration bundles/scripts.
- Roll-forward strategy.
- Backward-compatible schema changes.
- Expand/contract migrations.
- Large table migrations.
- Index creation without excessive locking.
- Rollback limitations.

Do NOT blindly run destructive migrations during application startup in production.

Explain exactly how migrations are applied in:

- Local development.
- Test environments.
- CI.
- Staging.
- Production.

---

# 18\. REDIS

Evaluate Redis for:

- Caching.
- Distributed coordination where appropriate.
- Rate limiting if appropriate.
- Frequently accessed reference data.

Do not use Redis as the source of truth for financial transactions.

Design:

- TTLs.
- Cache invalidation.
- Cache stampede protection where needed.
- Failure behavior.
- Serialization.
- Key naming.
- Memory limits.

The application must remain correct if Redis is unavailable unless Redis is explicitly part of a required consistency mechanism.

---

# 19\. RESILIENCE

Design for failure.

External dependencies can:

- Timeout.
- Return 500s.
- Return malformed data.
- Return 429.
- Become unavailable.
- Return partial data.
- Respond slowly.

Implement appropriate:

- Timeouts.
- Retries.
- Exponential backoff.
- Jitter.
- Circuit breakers.
- Bulkheads where justified.
- Cancellation tokens.
- Rate limiting.
- Backpressure.

Do not retry non-idempotent operations blindly.

Do not retry validation errors or permanent failures.

Document retry policies per dependency.

---

# 20\. SECURITY

Treat this as a banking system.

Implement defense in depth.

Consider:

- Authentication.
- Authorization.
- Object-level authorization.
- Least privilege.
- HTTPS.
- Secure headers.
- Input validation.
- Output encoding where applicable.
- SQL injection protection.
- SSRF protection where applicable.
- Rate limiting.
- Request size limits.
- File/resource access controls.
- Secret management.
- Encryption in transit.
- Encryption at rest.
- Key rotation.
- Audit logging.
- PII minimization.
- Sensitive data masking.
- Secure error handling.

Never put secrets in:

- Git.
- appsettings.json committed to source control.
- Docker images.
- source code.

Use appropriate secret management.

---

# 21\. SECURITY TESTING / BREACH TOOLS

Include a practical security testing strategy.

Evaluate appropriate tools such as:

- OWASP ZAP.
- Trivy.
- Snyk or equivalent dependency scanning.
- GitHub Dependabot.
- .NET security analyzers.
- Secret scanning.
- Container image scanning.
- SBOM generation.
- Static analysis.
- Dynamic API security testing.

Do not just list tools.

Integrate appropriate checks into CI/CD.

Explain what each tool protects against and what it does NOT protect against.

---

# 22\. AUTHORIZATION

Do not stop at authentication.

Design authorization around resources.

For example:

```
User A
  ↓
Customer A
  ↓
Account A
  ↓
Transactions A
```

User A must not be able to manipulate an ID in the URL and access Customer B's transactions.

Explicitly address IDOR/BOLA risks.

---

# 23\. LOGGING

Use structured logging.

Prefer:

```
TransactionSyncCompleted
ProviderRequestFailed
TransactionDuplicateDetected
EventProcessingFailed
```

with structured properties.

Do not log sensitive financial information unnecessarily.

Never log:

- Passwords.
- Tokens.
- Secrets.
- Full authentication credentials.
- Sensitive PII unnecessarily.

Include:

- Correlation ID.
- Trace ID.
- Event ID.
- Provider.
- Operation.
- Duration.
- Result.
- Error classification.

---

# 24\. OBSERVABILITY

Use OpenTelemetry where appropriate.

Provide:

- Logs.
- Metrics.
- Distributed traces.
- Health checks.

Important metrics could include:

```
events_received_total
events_processed_total
events_failed_total
duplicate_events_total
provider_failures_total
provider_latency
transaction_processing_latency
api_request_duration
api_error_rate
database_latency
queue_depth
```

Define useful dashboards/alerts conceptually.

Do not create meaningless metrics just to increase the metric count.

---

# 25\. HEALTH CHECKS

Implement:

```
/liveness
/readiness
/health
```

Distinguish:

- Process is alive.
- Application is ready to serve traffic.
- Dependency health.

Do not make liveness fail because an optional dependency such as Redis is unavailable.

Avoid health checks that cause cascading failures.

---

# 26\. PERFORMANCE

Treat performance as an engineering concern.

Consider:

- Database query performance.
- Indexes.
- Pagination.
- Connection pooling.
- Async I/O.
- Allocation reduction where justified.
- Serialization.
- Caching.
- Batch processing.
- Bulk inserts.
- Background processing.
- Provider concurrency limits.

Do not prematurely optimize.

Define performance assumptions and measurable targets.

Include a performance-testing strategy.

Consider tools such as:

- k6.
- NBomber.
- BenchmarkDotNet where appropriate.

Test realistic workloads.

---

# 27\. CONCURRENCY

Explicitly design for concurrent event processing.

Consider:

- Multiple consumers.
- Duplicate events.
- Same transaction arriving simultaneously.
- Optimistic concurrency.
- Database constraints.
- Transaction isolation.
- Race conditions.
- Distributed processing.

The correctness guarantee must come from the architecture and persistence layer, not from timing assumptions.

---

# 28\. DESIGN PATTERNS

Use design patterns only when they solve a real problem.

Potential patterns:

- Strategy.
- Adapter.
- Factory.
- Specification.
- Chain of Responsibility.
- Decorator.
- Outbox.
- Inbox.
- Unit of Work where appropriate.
- CQRS.
- Saga where genuinely required.

For every pattern, explain:

1. The problem.
2. Why the pattern solves it.
3. The alternative.
4. The trade-off.

Do not use patterns simply to demonstrate that you know patterns.

---

# 29\. SAGA

Evaluate whether a Saga is actually required.

Do NOT implement a Saga merely because the project is "production-grade."

A Saga is justified only when there is a distributed business workflow requiring multiple independently committed steps and compensating behavior.

If a Saga is unnecessary for the current transaction aggregation workflow, explicitly document that decision.

If a Saga becomes appropriate, design:

- Orchestration vs choreography.
- State transitions.
- Idempotency.
- Retry.
- Compensation.
- Timeout.
- Recovery.
- Observability.

---

# 30\. CLEAN CODE

Follow high-quality coding standards.

Use:

- Meaningful names.
- Small cohesive methods.
- Explicit dependencies.
- Immutable DTOs where appropriate.
- Guard clauses.
- Strong typing.
- Nullable reference types.
- Async/await correctly.
- CancellationToken propagation.
- No unnecessary abstractions.
- No magic strings.
- No magic numbers.
- No duplicated business logic.

Avoid "clever" code.

Optimize for maintainability.

---

# 31\. TESTING STRATEGY

Build a serious test pyramid.

Include:

### Unit tests

Test:

- Domain rules.
- Categorization.
- Result behavior.
- Validation.
- Business logic.

### Integration tests

Test:

- PostgreSQL.
- EF Core.
- Transactions.
- Constraints.
- Inbox/outbox.
- Redis where appropriate.
- Event processing.

Prefer real infrastructure through containers/Testcontainers where practical.

### API tests

Test:

- Authentication.
- Authorization.
- Validation.
- Pagination.
- Error responses.
- Problem Details.
- API contracts.

### Contract tests

Consider contracts between:

- Provider events.
- Internal events.
- API consumers.

### Architecture tests

Verify dependency boundaries.

Examples:

```
Domain → must not depend on Infrastructure
Domain → must not depend on ASP.NET
Application → must not depend on provider implementations
```

### Performance tests

Include realistic load scenarios.

### Security tests

Include automated security scanning and API security tests.

---

# 32\. CI/CD

Create a realistic pipeline.

Suggested flow:

```
Pull Request
    ↓
Build
    ↓
Format / Lint
    ↓
Static Analysis
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
Publish Artifact
    ↓
Deploy Staging
    ↓
Smoke Tests
    ↓
Production Deployment
```

Use appropriate deployment strategies.

Consider:

- Immutable artifacts.
- Environment configuration.
- Secrets injection.
- Database migration strategy.
- Roll-forward.
- Health checks.
- Deployment verification.
- Rollback strategy.

---

# 33\. DOCKER

Provide production-quality containers.

Consider:

- Multi-stage builds.
- Non-root execution.
- Minimal runtime image.
- Health checks.
- Read-only filesystem where practical.
- No secrets baked into images.
- Proper signal handling.
- Resource limits.
- Image scanning.

---

# 34\. .NET ASPIRE

Use .NET Aspire primarily for:

- Local orchestration.
- Service discovery.
- Developer experience.
- Observability.
- Local infrastructure management.

Do not make Aspire a hard runtime dependency unless there is a compelling reason.

The application should remain independently deployable.

Use Aspire to make local development easy:

```
API
PostgreSQL
Redis
Message Broker (if required)
Telemetry
```

---

# 35\. DOCKER COMPOSE

Provide Docker Compose for developers who do not use Aspire.

The local environment should be easy to start.

Example conceptual environment:

```
API
PostgreSQL
Redis
Message Broker (if required)
```

Keep local infrastructure minimal.

---

# 36\. CONFIGURATION

Use strongly typed options.

Separate:

- Application configuration.
- Environment configuration.
- Secrets.

Validate required configuration at startup.

Fail fast when critical configuration is missing.

Do not scatter configuration access throughout the codebase.

---

# 37\. DEPENDENCY MANAGEMENT

Keep dependencies minimal.

Before introducing a NuGet package, evaluate:

- Is it necessary?
- Is the project actively maintained?
- Is it secure?
- Does .NET already provide this capability?
- What is its operational cost?
- Does it lock us into a specific implementation?

Avoid dependency bloat.

---

# 38\. API DOCUMENTATION

Provide high-quality OpenAPI documentation.

Document:

- Endpoints.
- Authentication.
- Request parameters.
- Response schemas.
- Error responses.
- Pagination.
- Filtering.
- Examples.

The API should be understandable without reading the source code.

---

# 39\. ARCHITECTURAL DOCUMENTATION

Produce:

- System Context Diagram.
- Container Diagram.
- Component Diagram.
- Module Dependency Diagram.
- Database ERD.
- Event Flow Diagram.
- Sequence Diagrams for critical workflows.
- Deployment Diagram.
- Threat Model.
- Architecture Decision Records (ADRs).

Create ADRs for important decisions such as:

- Modular monolith vs microservices.
- PostgreSQL choice.
- Event broker choice.
- Inbox/outbox.
- CQRS.
- Redis.
- Partitioning.
- Migration strategy.
- Saga decision.
- Consistency model.

---

# 40\. THREAT MODEL

Perform a lightweight threat model using STRIDE or an equivalent methodology.

Consider:

- Spoofing.
- Tampering.
- Repudiation.
- Information disclosure.
- Denial of service.
- Elevation of privilege.

Identify threats around:

- Event ingestion.
- API.
- Provider integrations.
- Database.
- Redis.
- Message broker.
- Authentication.
- Authorization.
- Secrets.

For each important threat, provide:

```
Threat
Impact
Likelihood
Mitigation
Detection
Residual Risk
```

Do not claim the system is "100% secure."

---

# 41\. DATA RETENTION

Because this is financial data, consider:

- Retention periods.
- Audit requirements.
- Data minimization.
- Archiving.
- Deletion requirements.
- Legal/regulatory requirements.

Do not invent regulatory requirements.

Where requirements are unknown, explicitly state assumptions and identify what must be confirmed with compliance/legal teams.

---

# 42\. FAILURE SCENARIOS

Explicitly design and test scenarios such as:

1. PostgreSQL unavailable.
2. Redis unavailable.
3. Provider unavailable.
4. Provider timeout.
5. Provider returns 429.
6. Provider returns malformed event.
7. Duplicate event.
8. Same event processed concurrently.
9. Outbox publication fails.
10. Consumer crashes after database commit.
11. Consumer crashes before database commit.
12. Message broker unavailable.
13. Application restarts during processing.
14. Database migration fails.
15. Partial provider outage.
16. Large transaction volume.
17. Poison message.
18. Invalid authentication.
19. Unauthorized customer access.

For each scenario explain:

```
Detection
Response
Recovery
Data consistency
Observability
User impact
```

---

# 43\. MAINTAINABILITY

Optimize for low maintenance.

Prefer:

- Fewer moving parts.
- Clear boundaries.
- Automated deployment.
- Automated migrations.
- Automated security scanning.
- Strong defaults.
- Centralized observability.
- Minimal manual operational procedures.
- Boring infrastructure where possible.

Every new infrastructure component must have a documented justification.

---

# 44\. PRODUCTION READINESS CHECKLIST

Create a checklist covering:

Security\
 Reliability\
 Performance\
 Observability\
 Testing\
 Deployment\
 Database\
 Data protection\
 Disaster recovery\
 Configuration\
 Dependencies\
 Documentation\
 Operations

Identify:

- Must-have.
- Should-have.
- Future enhancement.

Do not pretend optional production infrastructure is mandatory for a small deployment.

---

# 45\. IMPLEMENTATION RULES

Before writing substantial code:

1. Analyze the requirements.
2. Identify assumptions.
3. Identify ambiguities.
4. Propose architecture.
5. Explain trade-offs.
6. Define module boundaries.
7. Define domain model.
8. Define event contracts.
9. Define database schema.
10. Define API contracts.
11. Define failure model.
12. Define security model.
13. Define testing strategy.
14. Define deployment strategy.
15. Then implement incrementally.

Do not generate the entire system in one enormous response.

Build in vertical slices.

After each major slice:

- Review architecture.
- Review security.
- Review tests.
- Review performance.
- Identify technical debt.
- Confirm consistency with previous decisions.

---

# 46\. IMPORTANT PRINCIPAL ENGINEER BEHAVIOR

Challenge my assumptions.

If I ask for something that is unnecessary, explain why.

If a simpler design is better, recommend the simpler design.

If a requirement introduces operational risk, explain it.

If two requirements conflict, identify the conflict and propose alternatives.

Do not blindly follow my requested technologies if they contradict the stated goals.

For example:

If I ask for Redis but Redis provides no meaningful value for a particular operation, say so.

If I ask for Kafka but a simpler mechanism is sufficient, explain the trade-off.

If I ask for a Saga but the workflow does not require distributed compensation, say so.

If partitioning is premature, say so.

If microservices are premature, say so.

The goal is **engineering correctness, not technology accumulation.**

---

# 47\. OUTPUT FORMAT

Start by producing:

## A. Executive Architecture Summary

Explain the architecture in concise Principal Engineer language.

## B. Architecture Diagram

Provide a Mermaid diagram.

## C. Module Boundaries

Show module responsibilities and dependencies.

## D. Event Flow

Show transaction event ingestion from provider to persistence.

## E. Database Design

Provide an ERD and explain normalization, indexes, constraints and partitioning decisions.

## F. API Design

List endpoints, contracts, pagination strategy and error model.

## G. Security Architecture

Provide authentication, authorization, threat model and security controls.

## H. Reliability Architecture

Explain retries, timeouts, circuit breakers, Inbox, Outbox, idempotency and failure handling.

## I. Observability

Explain logs, metrics, traces and health checks.

## J. Testing Strategy

Define unit, integration, contract, architecture, performance and security tests.

## K. CI/CD

Provide the proposed pipeline.

## L. ADRs

List the important architectural decisions and their trade-offs.

## M. Project Structure

Provide the proposed .NET solution/project structure.

## N. Implementation Roadmap

Break implementation into small vertical slices.

Do NOT start writing all production code until the architecture has been reviewed.

---

# FINAL QUALITY BAR

Judge every implementation against these questions:

1. Is it secure?
2. Is it correct under concurrency?
3. Is it resilient to dependency failure?
4. Is it observable?
5. Is it testable?
6. Is it maintainable?
7. Is it operationally simple?
8. Is it scalable where it needs to be?
9. Does the database enforce important invariants?
10. Are module boundaries respected?
11. Can providers evolve independently?
12. Can the system recover from partial failure?
13. Can engineers diagnose production problems?
14. Can the system be deployed repeatedly and safely?
15. Is every piece of complexity justified?

Most importantly:

**Do not confuse "production-grade" with "maximum complexity."**

The target architecture should be **boring, secure, observable, resilient, testable, maintainable and evolvable**.

Use complexity only where it buys measurable business or engineering value.

### One thing I'd add

Your original list is actually **very good**, but I'd make one major change in how you approach Claude:

**Don't tell it to implement all of those technologies immediately.**

For example, you've mentioned:

> Redis + Aspire + Docker \+ Docker Compose + Inbox \+ Outbox + Saga + partitioning \+ CQRS + Result + Clean Architecture + resilience...

That's potentially a **technology checklist**, rather than an architecture.

The strongest instruction in the prompt above is essentially:

> **"Challenge my assumptions. Don't introduce complexity without justification."**
