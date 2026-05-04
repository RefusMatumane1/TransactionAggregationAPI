# Transaction Aggregation API

A production-grade .NET 10 REST API that aggregates customer financial transactions from multiple external sources, categorizes them automatically using configurable keyword rules, and exposes rich querying and spend-analytics endpoints.

---

## Table of Contents

1. [Architecture](#architecture)
2. [Tech Stack](#tech-stack)
3. [Quick Start](#quick-start)
4. [Running Tests](#running-tests)
5. [API Reference](#api-reference)
6. [Categorization Rules](#categorization-rules)
7. [Background Sync](#background-sync)
8. [Trade-offs & Assumptions](#trade-offs--assumptions)
9. [What I Would Improve With More Time](#what-i-would-improve-with-more-time)

---

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                      HTTP Clients                           │
└────────────────────────────┬────────────────────────────────┘
                             │
┌────────────────────────────▼────────────────────────────────┐
│                TransactionAggregationAPI                    │
│   Minimal API endpoints  │  Rate Limiter  │  API Versioning │
│   GlobalExceptionHandler │  Serilog       │  OpenAPI/Scalar │
└────────────────────────────┬────────────────────────────────┘
                             │ MediatR — CQRS pipeline
┌────────────────────────────▼────────────────────────────────┐
│           TransactionAggregation.Application                │
│  Commands & Queries                                         │
│  Pipeline Behaviors: Logging│Validation│Caching│Idempotency │
│  CategorizationService  (keyword rules from appsettings)    │
│  AnalyticsService │ TransactionValidator                    │
└──────────────┬──────────────────────────┬───────────────────┘
               │ Domain types             │ Interfaces
┌──────────────▼────────────┐  ┌──────────▼───────────────────┐
│  Domain Layer             │  │  Infrastructure Layer         │
│  Transaction              │  │  BogusTransactionSource       │
│  Customer                 │  │  StaticDataTransactionSource  │
│  Account                  │  │  TransactionAggregator        │
│  Money / TransactionId /  │  │  RedisCacheService            │
│  AccountId  (Value Objs)  │  │  TransactionSyncBackground-   │
│  Domain Events            │  │    Service (scheduled worker) │
└──────────────┬────────────┘  └──────────┬───────────────────┘
               │                          │
┌──────────────▼──────────────────────────▼───────────────────┐
│           TransactionAggregation.Persistence                │
│  EF Core 10 + PostgreSQL (Npgsql)                           │
│  Migrations │ Entity Configurations │ Composite Indexes     │
└─────────────────────────────────────────────────────────────┘

Supporting Services (docker-compose)
  PostgreSQL  │  Redis  │  Seq  │  PgAdmin  │  Redis Commander
```

**Layer responsibilities:**

| Project | Responsibility |
|---|---|
| `Domain` | Entities, value objects, domain events, business rules |
| `Application` | CQRS handlers, pipeline behaviors, service interfaces |
| `Infrastructure` | External source adapters, aggregator, cache, background worker |
| `Persistence` | EF Core context, configurations, migrations |
| `TransactionAggregationAPI` | HTTP endpoints, middleware, DI wiring |

---

## Tech Stack

| Concern | Choice | Rationale |
|---|---|---|
| Web framework | ASP.NET Core 10 Minimal APIs | Low ceremony, fast startup, CQRS-friendly endpoint mapping |
| Mediator / CQRS | MediatR 14 | Clean command/query separation; composable pipeline behaviors for cross-cutting concerns |
| ORM | EF Core 10 + Npgsql | First-class PostgreSQL support, owned entities for value objects, automatic migrations |
| Database | PostgreSQL | ACID, JSONB for metadata, strong index options |
| Cache | Redis via StackExchange.Redis | Shared cache for idempotency keys and read-query results |
| Mapping | Mapster | Faster than AutoMapper; source-generator friendly |
| Validation | FluentValidation | Declarative, testable rules; wired into MediatR pipeline automatically |
| Logging | Serilog + Seq | Structured logs with correlation IDs; Seq makes them searchable |
| Observability | OpenTelemetry | Traces and metrics exportable to any OTLP backend |
| Resilience | Polly | Exponential-backoff retry on external source failures |
| Mock data | Bogus | Realistic fake transactions for BogusBank source |
| Testing | xUnit + FluentAssertions + NSubstitute + WebApplicationFactory | Unit + integration coverage with readable assertions |
| Containerization | Docker + docker-compose | One-command startup, health checks, service ordering |

---

## Quick Start

### Prerequisites

| Tool | Version |
|---|---|
| Docker Desktop | 24+ |
| Docker Compose | v2 |
| .NET SDK *(local dev only)* | 10.0 |

### Run with Docker (recommended)

```bash
git clone <repo-url>
cd TransactionAggregationAPI-main/TransactionAggregationAPI

docker compose up --build
```

| Service | URL | Notes |
|---|---|---|
| API | http://localhost:8080 | |
| OpenAPI / Scalar | http://localhost:8080/scalar/v1 | Development only |
| Health check | http://localhost:8080/health | |
| Liveness | http://localhost:8080/alive | |
| Seq (logs) | http://localhost:5341 | |
| pgAdmin | http://localhost:5050 | admin@transaction.com / admin |
| Redis Commander | http://localhost:8081 | |

```bash
# Stop
docker compose down

# Wipe all volumes (reset DB + cache)
docker compose down -v
```

### Run locally

```bash
# 1. Start only infrastructure
docker compose up postgres redis seq -d

# 2. Configure connection strings
#    Create TransactionAggregationAPI/appsettings.Development.json:
{
  "ConnectionStrings": {
    "transactiondb": "Host=localhost;Port=5432;Database=transactiondb;Username=postgres;Password=postgres",
    "redis": "localhost:6379",
    "seq": "http://localhost:5341"
  }
}

# 3. Run the API
cd TransactionAggregationAPI
dotnet run --project TransactionAggregationAPI/TransactionAggregationAPI.csproj
```

On startup the API automatically:
- Applies any pending EF Core migrations
- Seeds sample customers and transactions if the database is empty

---

## Running Tests

```bash
cd TransactionAggregationAPI-main/TransactionAggregationAPI

# All tests (unit + integration)
dotnet test

# Single test project
dotnet test TransactionAggregation.Tests/TransactionAggregation.Tests.csproj

# With coverage
dotnet test --collect:"XPlat Code Coverage"
```

**Test coverage breakdown:**

| Area | Type |
|---|---|
| Domain entities (`Transaction`, `Customer`, `Money`) | Unit |
| Categorization service | Unit |
| Transaction aggregator | Unit |
| Query handlers | Unit |
| API endpoints (health, customers, transactions) | Integration (WebApplicationFactory + in-memory DB) |

---

## API Reference

All endpoints are versioned under `/api/v1/`.

### Customers

| Method | Route | Description |
|---|---|---|
| `GET` | `/api/v1/customers` | List all (paginated, searchable) |
| `GET` | `/api/v1/customers/{id}` | Get by ID |
| `GET` | `/api/v1/customers/email/{email}` | Get by email |
| `POST` | `/api/v1/customers` | Create |
| `PUT` | `/api/v1/customers/{id}` | Update |
| `DELETE` | `/api/v1/customers/{id}` | Delete |

### Customer Transactions

| Method | Route | Description |
|---|---|---|
| `GET` | `/api/v1/customers/{id}/transactions` | Customer + transactions with aggregated totals |
| `GET` | `/api/v1/customers/{id}/transactions/filter` | **Rich filtered, paginated list** |
| `GET` | `/api/v1/customers/{id}/transactions/summary` | **Spend per category + monthly breakdowns** |
| `POST` | `/api/v1/customers/{id}/transactions` | Create a transaction manually |
| `POST` | `/api/v1/customers/{id}/transactions/sync` | Pull & sync from all external sources (idempotent) |

### Transactions

| Method | Route | Description |
|---|---|---|
| `GET` | `/api/v1/transactions/{id}` | Get single transaction |
| `PATCH` | `/api/v1/transactions/{id}/categorize` | Override category |

### Filter parameters (`/transactions/filter`)

| Param | Type | Default | Description |
|---|---|---|---|
| `pageNumber` | int | 1 | Page number |
| `pageSize` | int | 20 | Items per page (max 100) |
| `category` | enum | — | Filter by category |
| `status` | enum | — | Filter by status |
| `fromDate` | DateTime | — | Date range start |
| `toDate` | DateTime | — | Date range end |
| `minAmount` | decimal | — | Minimum absolute amount |
| `maxAmount` | decimal | — | Maximum absolute amount |
| `searchTerm` | string | — | Description or source text search |
| `source` | string | — | Exact source name match |
| `sortBy` | string | date | `date`, `amount`, `category`, `status`, `description` |
| `sortDescending` | bool | true | Sort direction |

### curl examples

```bash
# Create customer
curl -X POST http://localhost:8080/api/v1/customers \
  -H "Content-Type: application/json" \
  -d '{"email":"jane@example.com","name":"Jane Doe"}'

# Sync from external sources
curl -X POST "http://localhost:8080/api/v1/customers/{id}/transactions/sync?idempotencyKey=my-key-1"

# Filter: groceries in March, sorted by amount
curl "http://localhost:8080/api/v1/customers/{id}/transactions/filter?category=1&fromDate=2026-03-01&toDate=2026-03-31&sortBy=amount&sortDescending=true"

# Spend summary for last 3 months
curl "http://localhost:8080/api/v1/customers/{id}/transactions/summary?startDate=2026-01-01&endDate=2026-03-31"

# Re-categorize a transaction (Housing = 6)
curl -X PATCH http://localhost:8080/api/v1/transactions/{txId}/categorize \
  -H "Content-Type: application/json" \
  -d '{"category":6}'
```

### Transaction Categories

| Value | Name | Value | Name |
|---|---|---|---|
| 0 | Uncategorized | 7 | Healthcare |
| 1 | Groceries | 8 | Income |
| 2 | Dining | 9 | Transfer |
| 3 | Transportation | 10 | Shopping |
| 4 | Entertainment | 11 | Subscriptions |
| 5 | Utilities | | |
| 6 | Housing | | |

---

## Categorization Rules

Rules live in `appsettings.json` under `CategorizationRules.Keywords` — no code changes needed to add or modify them.

```json
"CategorizationRules": {
  "Keywords": {
    "walmart":   "Groceries",
    "uber":      "Transportation",
    "netflix":   "Entertainment",
    "rent":      "Housing",
    "starbucks": "Dining"
  }
}
```

**Algorithm:** case-insensitive substring match on the transaction description. First matching keyword wins. Positive-amount transactions default to `Income` if no keyword matches.

**Extensibility:** the `ITransactionCategorizationStrategy` interface allows swapping or chaining categorizers (e.g., plug in an ML model) without touching existing code.

---

## Background Sync

`TransactionSyncBackgroundService` is an `IHostedService` that wakes up every `TransactionSync:IntervalMinutes` minutes (default 60), iterates every customer, and pulls from all registered `ITransactionSource` providers.

- **Duplicate safety:** new transactions are filtered by `SourceExternalId` before inserting. A unique index at the DB level enforces this as a hard constraint.
- **Fault isolation:** a failure for one customer does not abort the others; each customer sync is wrapped in its own try/catch.
- **Graceful shutdown:** the worker respects `CancellationToken` and drains cleanly on SIGTERM.

Configure interval:

```json
"TransactionSync": {
  "IntervalMinutes": 60
}
```

---

## Trade-offs & Assumptions

| Decision | Rationale |
|---|---|
| PostgreSQL over SQL Server | JSONB metadata column, open source, excellent EF Core support |
| Minimal APIs over controllers | Less boilerplate; pairs naturally with CQRS endpoint handlers |
| In-memory dedup + DB unique index | HashSet for fast sync-time check; the index is the authoritative guard |
| Keyword scan (O(n) per transaction) | Fast enough for the current rule set size; Aho-Corasick trie would be needed at >1 000 rules |
| `Money` value object disallows zero amount | Every transaction must have economic value; `Account.Balance` stores plain `decimal` |
| Redis handles both idempotency and query caching | One external dependency for two purposes; TTLs are independent |
| Background service polls all customers | Simple and reliable; a message queue scales better for millions of customers |
| Mock sources use same DTO format | Keeps the demo clean; real integration would add a per-source adapter/normalizer |
| EF Core migrations applied at startup | Zero-ops deployment; acceptable trade-off is a brief startup delay on first run |

---

## What I Would Improve With More Time

1. **Third mock source with deliberately inconsistent formats** — a source that returns amounts as strings, dates in different timezones, and missing fields to demonstrate a robust adapter/normalizer pattern.

2. **Message queue for async ingestion** — replace the polling worker with RabbitMQ or Kafka. Each sync request becomes a message; worker pool processes them concurrently and scales horizontally.

3. **ML-ready categorization pipeline** — `ITransactionCategorizationStrategy` is in place. Adding an `MLTransactionCategorizationStrategy` (ML.NET or Azure Cognitive Services) would slot in without changing other code.

4. **Account balance mutations** — `Account` entity is modelled with `Credit`/`Debit` methods but not yet wired to transaction approval events. A domain service responding to `TransactionApprovedDomainEvent` would complete this.

5. **Cursor-based pagination** — current offset pagination degrades at large offsets. Keyset pagination using `(CreatedAt, Id)` as the cursor would be O(log n) on the composite index.

6. **Outbox pattern for domain events** — domain events are currently dispatched synchronously inside `SaveChangesAsync`. An outbox table would guarantee at-least-once delivery across transaction failures.

7. **CI/CD pipeline** — GitHub Actions workflow for build, test (with coverage gate), Docker image build and push, and rolling deploy to a container platform.

8. **Per-API-key rate limiting** — current limiter partitions by username/host. Integrating API key authentication and per-key rate limits would be appropriate for a multi-tenant production service.
