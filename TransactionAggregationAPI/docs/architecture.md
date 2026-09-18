# Architecture

Diagrams for the Transaction Aggregation API (Instructions.md section 39). These
describe what the code actually does; see the [ADRs](adr/README.md) for why
each decision was made, and the [threat model](threat-model.md) for security
analysis.

## System context

```mermaid
flowchart TB
    Customer([Customer]) -->|Blazor WASM UI, OIDC login| UI[Transaction Aggregation UI]
    Admin([Staff / Operator]) -->|Manage webhook sources| UI
    UI -->|REST + Bearer JWT| API[Transaction Aggregation API]
    Provider([Bank / Aggregator Provider]) -->|OAuth callback + webhook events| API
    API -->|Validate tokens| Keycloak[(Keycloak)]
    API -->|Read/write| Postgres[(PostgreSQL)]
    API -->|Cache, rate limits, OAuth state| Redis[(Redis)]
    API -->|Structured logs| Seq[(Seq)]
    API -->|Traces/metrics| Otel[(OpenTelemetry Collector)]
```

## Container / module diagram

One deployable process (modular monolith — [ADR-0001](adr/0001-modular-monolith-not-microservices.md)),
with internal module boundaries enforced by `LayerDependencyTests`, not network
boundaries.

```mermaid
flowchart TB
    subgraph Host["TransactionAggregationAPI (single process)"]
        direction TB
        Endpoints["API layer\nMinimal API endpoints, DTOs, ProblemDetails, versioning"]
        Application["Application layer\nCQRS handlers (MediatR), validators,\npipeline behaviors, Result pattern"]
        Domain["Domain layer\nEntities, value objects, domain events\n(zero EF Core / ASP.NET references)"]
        Infrastructure["Infrastructure layer\nInbox/Outbox dispatchers, Redis cache,\nKeycloak admin client, bank aggregator client"]
        Persistence["Persistence layer\nEF Core DbContext, entity configurations,\nmigrations"]

        Endpoints --> Application
        Application --> Domain
        Infrastructure --> Domain
        Persistence --> Domain
        Application -.->|via IApplicationDbContext port| Persistence
        Application -.->|via ports: ICacheService, IBankAggregatorClient, etc.| Infrastructure
    end

    Host --> PG[(PostgreSQL)]
    Host --> Redis[(Redis)]
    Host --> KC[(Keycloak)]
```

Dependency direction matches the brief's preferred shape (API → Application →
Domain → Ports → Infrastructure Adapters): the Domain project has no reference
to EF Core, ASP.NET Core, or provider-specific packages, and Application only
depends on ports (interfaces) it defines, never on Infrastructure/Persistence
implementations directly.

## Event flow: transaction ingestion

```mermaid
flowchart LR
    Provider([Bank aggregator]) -->|POST webhook, X-Api-Key| Auth{API key valid\n& source active?}
    Auth -->|No| Reject[401]
    Auth -->|Yes| Validate{Schema valid,\n≤500 transactions?}
    Validate -->|No| BadRequest[400]
    Validate -->|Yes| Inbox["Write InboxMessage\n(202 Accepted immediately)"]
    Inbox --> Dispatcher["InboxDispatcherBackgroundService\npolls Status+NextAttemptAt"]
    Dispatcher --> Command["ProcessInboundTransactionsCommand"]
    Command --> Dedup{"External ID\nalready exists for\nthis customer?"}
    Dedup -->|Yes| Skip["Skip — no-op success\n(idempotent, see ADR-0002)"]
    Dedup -->|No| Categorize["Rule-based categorization"]
    Categorize --> Persist["Persist Transaction +\nOutboxMessage\n(same DB transaction)"]
    Persist --> OutboxDispatch["OutboxDispatcherBackgroundService\npolls, publishes integration event"]
```

Idempotency is enforced by a database-level unique constraint on
`(CustomerId, Source.ExternalId)`, not just the in-handler dedup check shown
above — see [ADR-0002](adr/0002-postgresql-as-system-of-record.md) for why the
application-level check alone isn't sufficient under concurrent delivery.

## Sequence: bank-link OAuth completion

```mermaid
sequenceDiagram
    actor Customer
    participant UI as Transaction UI
    participant API
    participant Cache as Redis
    participant Provider as Bank Aggregator
    participant DB as PostgreSQL

    Customer->>UI: Click "Link my bank"
    UI->>API: POST /bank-links (InitiateBankLink)
    API->>DB: Create/reset BankLink (PendingAuthorization)
    API->>Cache: Store OAuth state (10 min TTL)
    API->>DB: Save
    API-->>UI: Authorization URL
    UI->>Provider: Redirect customer
    Provider->>Customer: Consent screen
    Provider->>UI: Redirect back with code + state
    UI->>API: POST /bank-links/complete (CompleteBankLink)
    API->>Cache: Look up + remove state (single use)
    alt state invalid/expired
        API-->>UI: 400 Validation error
    else state valid
        API->>Provider: Exchange authorization code
        Provider-->>API: Access/refresh tokens
        API->>Provider: Fetch linked account details
        API->>DB: Create Account (or reuse if already exists),\nActivate BankLink with encrypted tokens
        API-->>UI: 200 OK, accountId
    end
```

## Database ERD

```mermaid
erDiagram
    Customer ||--o{ Account : owns
    Customer ||--o{ BankLink : has
    Customer ||--o{ Transaction : has
    Account ||--o{ Transaction : "posted to (nullable)"
    BankLink }o--|| Account : "linked to (nullable until activated)"

    Customer {
        guid Id PK
        string Email UK
        string Name
        datetime CreatedAt
        datetime UpdatedAt
    }
    Account {
        guid Id PK
        guid CustomerId FK
        string AccountNumber
        string AccountName
        int AccountType
        decimal Balance
        string Currency
        bool IsActive
    }
    Transaction {
        guid Id PK
        guid CustomerId FK
        guid AccountId "FK, nullable"
        decimal Amount
        string Currency
        string Description
        int Category
        string SourceName
        string SourceExternalId "unique with CustomerId"
        int Status
        datetime Date
    }
    BankLink {
        guid Id PK
        guid CustomerId FK
        guid AccountId "FK, nullable until activated"
        int Institution
        int Status
        string ExternalAccountId
        string EncryptedAccessToken
        string EncryptedRefreshToken
        datetime TokenExpiresAt
    }
    WebhookSource {
        guid Id PK
        string Name UK
        string KeyHash
        bool IsActive
        datetime LastUsedAt
    }
    InboxMessage {
        guid Id PK
        string SourceName
        string Payload
        int Status
        datetime NextAttemptAt
    }
    OutboxMessage {
        guid Id PK
        string Type
        string Payload
        int Status
        datetime NextAttemptAt
    }
```

`Money` (Amount/Currency) and `TransactionSource` (SourceName/SourceExternalId)
are value objects owned by `Transaction`, not separate tables — see
[ADR-0002](adr/0002-postgresql-as-system-of-record.md) for the decimal-currency
rationale. `InboxMessage`/`OutboxMessage` are deliberately not foreign-keyed to
the entities they describe — see [ADR-0003](adr/0003-polling-inbox-outbox-not-a-broker.md).

## Deployment (Kubernetes)

```mermaid
flowchart TB
    subgraph Cluster["Kubernetes namespace: transaction-aggregation"]
        Migrate["db-migrate Job\n(Helm pre-install/upgrade hook,\nadvisory-lock serialized)"]
        subgraph APIPods["transaction-api Deployment (2 replicas)"]
            Pod1[API pod 1]
            Pod2[API pod 2]
        end
        UIPods["transaction-ui Deployment"]
        PG[(PostgreSQL)]
        Redis[(Redis)]
        KC[(Keycloak)]
        Seq[(Seq)]
        Prom[(Prometheus)]
    end

    Migrate -->|Runs to completion first| PG
    APIPods -->|readiness/liveness probes| APIPods
    Pod1 --> PG
    Pod2 --> PG
    Pod1 --> Redis
    Pod2 --> Redis
    Pod1 --> KC
    Pod2 --> KC
    UIPods --> Pod1
    UIPods --> Pod2
    Prom -.->|scrape /metrics| Pod1
    Prom -.->|scrape /metrics| Pod2
```

`db-migrate` runs to completion (Helm hook) before the API Deployment rolls
out — no pod self-migrates in Staging/Production (see the `Program.cs`
`IsDevelopment()` gate and the comment in `k8s/api/deployment.yaml`).
`RollingUpdate` with `maxUnavailable: 0` keeps at least 2 pods serving traffic
during a deploy; the readiness probe (`/health`) keeps a pod out of rotation
until Postgres is reachable, while Redis failures only degrade (not fail) it —
see [ADR-0005](adr/0005-redis-cache-only.md).
