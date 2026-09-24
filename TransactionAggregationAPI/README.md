# Transaction Aggregation API

A production-grade .NET 10 system that aggregates customer financial transactions
from multiple bank sources, categorises spending automatically, and exposes a
comprehensive versioned REST API. Comes with a Blazor WASM frontend and ships
with Docker Compose, .NET Aspire, and Kubernetes manifests for three different
ways to run it locally.

---

## Table of contents

1. [What this project does](#what-this-project-does)
2. [Tech stack](#tech-stack)
3. [Project structure](#project-structure)
4. [Seed data (test accounts)](#seed-data)
   - [Mock bank feeds (development)](#mock-bank-feeds-development)
5. [Option A — Docker Compose](#option-a--docker-compose-quickest)
6. [Option B — .NET Aspire (debug)](#option-b--net-aspire-debug)
7. [Option C — Kubernetes](#option-c--kubernetes-rancher-desktop)
8. [API reference](#api-reference)
9. [Configuration reference](#configuration-reference)
10. [Troubleshooting](#troubleshooting)

---

## What this project does

- Aggregates transactions from multiple banks (mock FNB, Absa, Capitec and Standard Bank feeds in development), received by REST webhook or Kafka, with idempotent ingestion
- Categorises transactions automatically by keyword (Groceries, Dining, Transport …)
- Exposes a versioned REST API (`/api/v1/…`) secured with Keycloak-issued JWT bearer tokens
- Caches query results in Redis to reduce database round-trips
- Enforces distributed rate limiting across all replicas via Redis
- Emits structured logs to Seq and traces via OpenTelemetry
- Exposes a Prometheus `/metrics` scrape endpoint via prometheus-net
- Ships with Grafana dashboards pre-provisioned with HTTP, runtime, and GC panels
- Ships with a Blazor WASM frontend served by nginx in production
- Runs EF Core migrations automatically on startup

---

## Tech stack

| Layer | Technology |
|---|---|
| Runtime | .NET 10 / ASP.NET Core Minimal API |
| Frontend | Blazor WebAssembly (.NET 10) |
| Database | PostgreSQL 16 + Entity Framework Core 10 |
| Cache / Rate limiting | Redis 7 + StackExchange.Redis |
| Structured logging | Serilog → Seq |
| Traces | OpenTelemetry (ASP.NET Core + EF Core + Redis + HttpClient) |
| Metrics | prometheus-net.AspNetCore → Prometheus → Grafana |
| Auth | Keycloak (OIDC) — Authorization Code + PKCE |
| API docs | Scalar (OpenAPI — Development only) |
| Orchestration | Docker Compose · .NET Aspire · Kubernetes (k3s / Rancher Desktop) |

---

## Project structure

```
TransactionAggregationAPI/              ← solution root
│
├── TransactionAggregationAPI/          ← Web API (entry point, also hosts Blazor in dev)
│   ├── Middleware/                     ← Exception handling, request context logging
│   ├── RateLimiting/                   ← Redis-backed custom RateLimiter
│   ├── SeedData.cs                     ← 10 customers, 21 accounts, ~2 700 transactions
│   ├── Program.cs                      ← App bootstrap
│   └── appsettings.json
│
├── TransactionAggregationUI/           ← Blazor WASM frontend
│   ├── Pages/                          ← Dashboard, Accounts, Transactions, Register, Authentication …
│   ├── Services/                       ← HTTP clients for each API resource
│   ├── Auth/                           ← Claims helper for the OIDC-issued principal
│   ├── wwwroot/appsettings.json        ← ApiBaseUrl, Keycloak:Authority (templated at container start)
│   ├── nginx.conf                      ← Proxies /api/ to the API service
│   └── Dockerfile                      ← nginx + published WASM static files
│
├── TransactionAggregation.MockAggregator/ ← Development only: stand-in account aggregator — bank-link consent (OAuth) and
│                                         mock FNB/Absa/Capitec/Standard Bank feeds pushed through the real webhook or Kafka
├── TransactionAggregation.Worker/      ← Background-processing host: Kafka consumer + inbox/outbox/pending-expiry dispatchers
│   └── Dockerfile                      ← Scales independently of the API (serves only /alive, /health, /metrics)
│
├── TransactionAggregation.Hosting/     ← Composition shared by the API and the worker (modules, Postgres, Redis cache,
│                                         Data Protection key ring, Serilog, categorization-rules.json)
│
├── TransactionAggregationAPI.AppHost/  ← .NET Aspire orchestration
│   ├── AppHost.cs                      ← Wires up API + worker + Postgres + Redis + Seq + Kafka
│   └── appsettings.Development.json    ← Fixed postgres-password parameter
│
├── Modules/Transactions/Transactions.Application/    ← Transaction use cases (CQRS / MediatR), categorization
├── Modules/Transactions/Transactions.Domain/         ← Transaction entity, value objects, domain events
├── Modules/Transactions/Transactions.Infrastructure/ ← TransactionsDbContext (`transactions` schema), migrations, inbox/outbox dispatchers (run by the worker)
├── TransactionAggregationAPI.ServiceDefaults/ ← Health checks, OpenTelemetry, prometheus-net
│
├── BuildingBlocks/                     ← Shared technical infrastructure; depends on no module
│   ├── SharedKernel/                   ← Common building blocks every module depends on (Result, ICommand/IQuery, ValueObject, MediatR pipeline behaviors)
│   ├── BuildingBlocks.Messaging/       ← Generic Inbox/Outbox reliability mechanism — own DbContext, `messaging` schema
│   └── BuildingBlocks.Web/             ← HTTP helpers every module's Presentation reuses (Result → ProblemDetails, versioned route groups,
│                                         customer-ownership filter, shared policy names)
├── Modules/                            ← Each module: Domain · Application · Infrastructure · Presentation (+ Contracts for other modules)
│   │                                     Presentation = the module's HTTP surface: one file per endpoint under Endpoints/, wire shapes under
│   │                                     Requests/ and Responses/. Application DTOs are always mapped to a response, never serialized
│   │                                     directly. Referenced only by the API host — the worker runs every module without it.
│   ├── WebhookSources/                 ← First fully-extracted module — own DbContext, `webhooksources` schema, depends only on SharedKernel
│   ├── Audit/                          ← Append-only inbound audit trail — own DbContext, `audit` schema, depends on no other module (ADR-0011)
│   └── BankLinks/                      ← Second extracted module — own DbContext, `banklinks` schema, depends only on SharedKernel; needs Account via a consumer-owned port (ADR-0010)
│
├── monitoring/                         ← Docker Compose monitoring stack
│   ├── prometheus.yml                  ← Prometheus scrape config (api:8080 and every worker replica's /metrics)
│   └── grafana/
│       ├── provisioning/               ← Auto-provisioned datasource + dashboard provider
│       └── dashboards/                 ← transaction-api.json Grafana dashboard
│
├── docker-compose.yml                  ← Full local stack including Prometheus + Grafana
├── docker-compose.override.yml         ← Dev overrides (user secrets, ports)
├── deploy-k8s.sh                       ← One-command Kubernetes deployment script
│
└── k8s/                                ← Kubernetes manifests
    ├── namespace.yaml
    ├── secrets.yaml                    ← Fill in before applying
    ├── configmap.yaml                  ← API environment variables
    ├── network-policy.yaml             ← Pod-level traffic rules
    ├── postgres/ redis/ seq/ kafka/    ← StatefulSets + Services (persistent volumes)
    ├── api/                            ← Deployment, Service, Ingress, HPA, PDB, migration Job
    ├── ui/                             ← Deployment, Service, Ingress, ConfigMap
    ├── dev-tools/                      ← pgAdmin, Redis Commander, Kafka UI (optional --dev-tools)
    ├── monitoring/                     ← Prometheus + Grafana (optional --monitoring)
    │   ├── prometheus/                 ← ServiceAccount, RBAC, ConfigMap, Deployment, Service, Ingress
    │   └── grafana/                    ← ConfigMaps (provisioning + dashboards), PVC, Deployment, Service, Ingress
    └── helm/                           ← Helm chart + per-environment values
```

---

## Seed data

On first start the API seeds the database with **10 realistic South African customers**,
**21 bank accounts** (checking, savings, credit card, investment), and **~2 700 transactions**
spread across January 2025 → April 2026 so all date filters return results out of the box.

All seed accounts share the same password:

| Field | Value |
|---|---|
| Password | `Test@12345` |

Sample logins:

| Name | Email |
|---|---|
| Thabo Mokoena | `thabo.mokoena@example.co.za` |
| Lerato Dlamini | `lerato.dlamini@example.co.za` |
| Pieter van der Merwe | `pieter.vandermerwe@example.co.za` |
| Ayanda Zulu | `ayanda.zulu@example.co.za` |
| Fatima Ismail | `fatima.ismail@example.co.za` |

### Mock bank feeds (development)

The seed data above is written straight into the database. To exercise the **real**
pipeline — linking, ingestion, normalization, customer resolution, categorization —
`TransactionAggregation.MockAggregator` stands in for the external account aggregator in
Docker Compose and Aspire (never in Kubernetes). It does two jobs:

1. **Bank-link consent.** It implements the aggregator's OAuth endpoints, so the bank-link
   flow works end to end. Without it, no account can be linked in development.
2. **Bank feeds.** Every 30 s it pushes new transactions for each account a customer has
   linked, through the API's webhook (`Feed__Channel=Kafka` sends to the topic instead).
   Some card purchases arrive `pending` and post on a later tick (restaurants with a tip
   added); now and then a whole batch is re-sent, to exercise duplicate detection.

Each mock bank writes transactions its own way. **These formats are invented for the mock**;
their normalization rules live in `normalization-rules.Development.json`, which only
Development loads:

| Mock bank | Description as sent | Date as sent | Category as sent |
|---|---|---|---|
| FNB | `POS PURCHASE  WOOLWORTHS  SANDTON` | local time, no offset | its own labels (`Takeaways`, `Petrol`, …) |
| Absa | `ABSA CARD Woolworths Sandton` | `+02:00` offset | none |
| Capitec | `Woolworths Sandton` | UTC | our category names |
| Standard Bank | `PURCHASE Woolworths` | local time, no offset | none — no profile, so only the default rules apply ("Other") |

**Link an account and watch transactions arrive:**

1. Sign in to the UI as a seed customer and open **Linked banks** in the sidebar.
2. Choose **Link** on a bank. You're sent to the mock's consent page, which lists that
   bank's accounts; choose one and **Allow**.
3. You land back on the UI, which completes the link. Within one feed interval the
   account's transactions appear under **Accounts**.

**Deny** returns you to the UI with nothing linked; a bank whose linking was abandoned
shows **Try again**.

The whole flow — with sequence diagrams, the stage-by-stage pipeline, and the terminal
commands to verify it — is in [docs/bank-feed-flow.md](docs/bank-feed-flow.md).

To see [joint accounts](#joint-accounts) work, link **FNB Joint Household Account** while
signed in as two different customers: each receives every transaction. `GET http://localhost:5090/consents`
lists the accounts being fed; `DELETE /consents/{accountId}` stops one.

The two apps share a client secret and the webhook source's API key (development values in
`appsettings.Development.json`, the AppHost parameters and `docker-compose.yml`); in
Development the API registers the `mock-aggregator` webhook source with that key at startup.

### Admin login

A staff account with the `admin` realm role is baked into the Keycloak realm import (both
docker-compose and k8s), so `/admin/webhook-sources` is reachable without hand-editing Keycloak:

| Field | Value |
|---|---|
| Email | `admin@transaction.local` |
| Password | `Admin@12345` |

Rotate/replace this before anything beyond a local/ephemeral cluster — see
`keycloak/realm-export.json`.

---

## Option A — Docker Compose (quickest)

One command starts everything: API, UI, PostgreSQL, Redis, Seq, pgAdmin,
Redis Commander, Prometheus, and Grafana. Migrations and seeding run automatically
on first start.

### Prerequisites

- [Docker Desktop](https://www.docker.com/products/docker-desktop) running

### Run

```bash
cd /path/to/TransactionAggregationAPI

docker-compose up --build
```

### What's running

| Service | URL |
|---|---|
| **UI** (Blazor frontend) | http://localhost:7200 |
| **API** | http://localhost:5001 |
| **API docs** (Scalar) | http://localhost:5001/scalar/v1 |
| **Seq** structured logs | http://localhost:5341 |
| **Prometheus** | http://localhost:9090 |
| **Grafana** | http://localhost:3000 (admin / admin) |
| **pgAdmin** | http://localhost:5050 |
| **Redis Commander** | http://localhost:8082 |
| **Kafka UI** | http://localhost:8083 |
| **Kafka broker** (from the host) | `localhost:9092` |
| **Mock aggregator** (bank-link consent) | http://localhost:5090 |

> **pgAdmin first-time setup:** login `admin@transaction.com` / `admin`,
> add server → host `postgres`, port `5432`, user `postgres`, password `postgres`.

> **Grafana:** open the pre-provisioned "Transaction Aggregation API" dashboard.
> Data appears within ~30 seconds after the API starts serving requests.

### Stop

```bash
docker-compose down          # stop and keep all data volumes
docker-compose down -v       # stop and delete all data
```

---

## Option B — .NET Aspire (debug)

Use Aspire when you want hot-reload, breakpoints, and the Aspire dashboard
showing every service's health, logs, and traces in one place.

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker Desktop](https://www.docker.com/products/docker-desktop) (for containers)
- .NET Aspire workload:

```bash
dotnet workload install aspire
```

### Run

```bash
cd /path/to/TransactionAggregationAPI

dotnet run --project TransactionAggregationAPI.AppHost
```

Aspire starts PostgreSQL, Redis, and Seq as containers, then launches the API
project. The Aspire dashboard opens automatically in your browser.

### What's running

| Service | URL |
|---|---|
| **Aspire dashboard** | Printed in terminal on startup (e.g. `https://localhost:17138`) |
| **UI + API** | https://localhost:5101 (sign in here — Keycloak only accepts this origin under Aspire) |
| **API docs** (Scalar) | https://localhost:5101/scalar/v1 |
| **Seq** structured logs | Linked from Aspire dashboard |
| **Kafka UI** | http://localhost:8083 |
| **Mock aggregator** (bank-link consent) | http://localhost:5090 |

> Aspire uses a fixed PostgreSQL password (`postgres`) stored in
> `TransactionAggregationAPI.AppHost/appsettings.Development.json` so
> the data volume survives restarts without authentication errors.

### Attach a debugger

Open the solution in Visual Studio or Rider, set the AppHost as the startup
project, and press **F5**. All projects in the Aspire graph are debuggable.

---

## Option C — Kubernetes (Rancher Desktop)

Runs the full production-like stack on a local single-node k3s cluster.
This is the closest to a real staging or production deployment.

### What gets deployed

| Component | Replicas | Notes |
|---|---|---|
| **API** (.NET 10) | 2 | Auto-scales to 10 via HPA |
| **PostgreSQL 16** | 1 | StatefulSet + 10 Gi PVC |
| **Redis 7** | 1 | StatefulSet + 2 Gi PVC, AOF persistence |
| **Seq** | 1 | StatefulSet + 5 Gi PVC; access via port-forward (no Ingress) |
| **Kafka** (KRaft, single node) | 1 | StatefulSet + 5 Gi PVC; `bank-transactions` topic (6 partitions) + `.dlq` |
| **UI** (nginx + Blazor WASM) *(`--ui`)* | 2 | Serves frontend, proxies `/api/` to the API service |
| **Prometheus** *(`--monitoring`)* | 1 | Pod annotation-based scrape discovery |
| **Grafana** *(`--monitoring`)* | 1 | Pre-provisioned dashboard + Prometheus datasource |
| **pgAdmin** *(`--dev-tools`)* | 1 | PostgreSQL browser |
| **Redis Commander** *(`--dev-tools`)* | 1 | Redis key browser |
| **Kafka UI** *(`--dev-tools`)* | 1 | Browse topics, produce test records |

### Prerequisites

**1. Install [Rancher Desktop](https://rancherdesktop.io)**
(ships with `kubectl`, `helm`, `nerdctl`, and a k3s cluster)

**2. In Rancher Desktop → Preferences → Container Engine → select `containerd`**
(`nerdctl` requires containerd, not dockerd)

**3. Verify the cluster is ready:**

```bash
kubectl get nodes
# NAME                   STATUS   ROLES                  VERSION
# lima-rancher-desktop   Ready    control-plane,master   v1.33.x+k3s1

kubectl config current-context
# rancher-desktop
```

If the context is wrong:
```bash
kubectl config use-context rancher-desktop
```

### Step 1 — Build the images

k3s uses its own containerd image store (`k8s.io` namespace). Images built with
plain `docker build` are invisible to the cluster. Build directly into it:

```bash
cd /path/to/TransactionAggregationAPI

# Images are tagged with the commit they were built from; deploy-k8s.sh deploys exactly
# that tag (IMAGE_TAG, default: the current git short sha) — never a moving :latest.
IMAGE_TAG=$(git rev-parse --short HEAD)

# API image (also runs the db-migrate Job)
nerdctl --namespace k8s.io build \
  -t transactionaggregationapi:$IMAGE_TAG \
  -f TransactionAggregationAPI/Dockerfile \
  .

# Worker image
nerdctl --namespace k8s.io build \
  -t transactionaggregationworker:$IMAGE_TAG \
  -f TransactionAggregation.Worker/Dockerfile \
  .

# UI image (only needed if you plan to pass --ui)
nerdctl --namespace k8s.io build \
  -t transactionaggregationui:$IMAGE_TAG \
  -f TransactionAggregationUI/Dockerfile \
  .

# Confirm images are present
nerdctl --namespace k8s.io images | grep transaction
```

### Step 2 — Fill in secrets

Open `k8s/secrets.yaml` and replace the `CHANGE_ME_BEFORE_DEPLOY` values:

```bash
# PostgreSQL password
echo -n 'YourStrongPassword123!' | base64

# Keycloak admin console password
echo -n 'YourAdminPassword!' | base64

# pgAdmin password
echo -n 'YourAdminPassword!' | base64
```

`keycloak-admin-client-secret` is already correct for a fresh local/ephemeral cluster — it
matches the `transaction-admin` client secret baked into `k8s/keycloak/configmap.yaml`'s realm
export. If you rotate one, rotate both (see the comment next to it in `secrets.yaml`).

> **Never commit `secrets.yaml` with real values.**
> Protect it: `git update-index --assume-unchanged k8s/secrets.yaml`

### Step 3 — Run the deploy script

The `deploy-k8s.sh` script handles the entire deployment in the correct order,
waits for each step to finish, updates `/etc/hosts`, and prints a verification
summary at the end.

```bash
# Core stack only (API + data stores)
./deploy-k8s.sh

# Also deploy the Blazor WASM UI
./deploy-k8s.sh --ui

# Also deploy Prometheus + Grafana
./deploy-k8s.sh --monitoring

# Also deploy pgAdmin + Redis Commander
./deploy-k8s.sh --dev-tools

# Deploy all optional components
./deploy-k8s.sh --ui --monitoring --dev-tools

# Skip the /etc/hosts update (if you manage it manually)
./deploy-k8s.sh --skip-hosts

# Remove everything (deletes all data)
./deploy-k8s.sh --teardown
```

The script runs these phases automatically:

| Phase | Action |
|---|---|
| Pre-flight | Checks kubectl, cluster reachability, images in the k8s.io namespace, and that secrets.yaml is populated |
| Namespace | Creates the `transaction-aggregation` namespace |
| Secrets & ConfigMaps | Applies secrets and API ConfigMap; UI ConfigMap *(if `--ui`)* |
| Data stores | Deploys PostgreSQL, Redis, Seq StatefulSets — waits for both PostgreSQL and Redis to be Ready |
| API | Deploys Service, Deployment, Ingress, HPA, PDB — waits for the readiness probe (`/health`) |
| UI *(if `--ui`)* | Deploys Deployment, Service, Ingress — waits for readiness |
| Network policies | Applies pod-level traffic rules |
| Dev tools *(if `--dev-tools`)* | Deploys pgAdmin and Redis Commander |
| Monitoring *(if `--monitoring`)* | Applies Prometheus RBAC then Deployment + Service + Ingress; Grafana ConfigMaps, PVC, Deployment, Service, Ingress |
| /etc/hosts | Adds hostnames for all deployed services via sudo (skips entries already present) |

> EF Core migrations run automatically inside the API pod at startup (`ApplyMigrationsAsync`).
> There is no separate migration step. `k8s/api/migration-job.yaml` exists as an alternative
> if you ever need to decouple migrations from app startup.

### Step 4 — Open the app

Once the script completes:

| Service | URL |
|---|---|
| **UI** *(if `--ui`, start here)* | http://ui.transaction.local |
| **API** | http://api.transaction.local |
| **Health check** | http://api.transaction.local/health |
| **Metrics** | http://api.transaction.local/metrics |
| **Prometheus** *(if `--monitoring`)* | http://prometheus.transaction.local |
| **Grafana** *(if `--monitoring`)* | http://grafana.transaction.local (admin / admin) |
| **pgAdmin** *(if `--dev-tools`)* | http://pgadmin.transaction.local |
| **Redis Commander** *(if `--dev-tools`)* | http://redis-commander.transaction.local |
| **Kafka UI** *(if `--dev-tools`)* | http://kafka-ui.transaction.local |

> Seq has no Kubernetes Ingress — use port-forward to access logs:
> `kubectl port-forward svc/seq 5341:80 -n transaction-aggregation`

> Scalar API docs (`/scalar/v1`) are not available in Kubernetes — the ConfigMap
> sets `ASPNETCORE_ENVIRONMENT=Production`. Use Option A or B for API exploration.

**On Windows (Rancher Desktop): add the hostnames to `C:\Windows\System32\drivers\etc\hosts` as `127.0.0.1` entries** — see the troubleshooting section below for the exact lines.

**No hostnames working? Use port-forward instead:**

```bash
kubectl port-forward svc/transaction-api  8080:80    -n transaction-aggregation
kubectl port-forward svc/transaction-ui   7200:80    -n transaction-aggregation  # if --ui (service port 80 → container 8080)
kubectl port-forward svc/seq              5341:80    -n transaction-aggregation  # logs (no Ingress)
kubectl port-forward svc/prometheus       9090:9090  -n transaction-aggregation  # if --monitoring
kubectl port-forward svc/grafana          3000:80    -n transaction-aggregation  # if --monitoring
kubectl port-forward svc/kafka            9092:9092  -n transaction-aggregation  # produce to localhost:9092
kubectl port-forward svc/kafka-ui         8083:8080  -n transaction-aggregation  # if --dev-tools
```

### Rebuilding after a code change

Commit, rebuild the changed images with the new `IMAGE_TAG` (Step 1), and re-run
`./deploy-k8s.sh` (with `--ui` if the UI changed). The script always runs the `db-migrate`
Job first and waits for it to complete, so a new EF Core migration is applied before any pod
runs the code that needs it. No pod applies migrations itself outside Development; see
`k8s/api/migration-job.yaml`. If the Job fails, the script stops before touching the API
and prints the Job's logs.

### Tear down

```bash
./deploy-k8s.sh --teardown
# or manually:
kubectl delete namespace transaction-aggregation
```

---

## API reference

### Authentication

Keycloak is the identity provider — this API never issues or verifies passwords itself, it only
validates the JWTs Keycloak signs. All endpoints except registration require a bearer token:

```
Authorization: Bearer <token>
```

**Register a new account:**

```http
POST /api/v1/customers
Content-Type: application/json

{
  "name": "Jane Smith",
  "email": "jane@example.com",
  "password": "SecurePassword123!"
}
```

This creates the user directly in Keycloak (via its Admin API, using the confidential
`transaction-admin` service-account client) and a matching local `Customer` row using Keycloak's
own user id — see `IKeycloakAdminClient`/`CreateCustomerCommandHandler`.

**Log in:**

The API has no `/login` endpoint. The Blazor UI signs users in with Keycloak's own hosted login
page via the Authorization Code + PKCE flow (redirecting to
`http://localhost:8081/realms/transaction-aggregation/...` in the docker-compose setup, or
`http://keycloak.transaction.local/...` in k8s). `transaction-ui` only allows that flow (no
direct password grant, by design) — to call the API directly (a script, Postman, etc.) without a
browser, complete that same redirect flow yourself (e.g. with a PKCE-capable HTTP client), or add
a separate Keycloak client with direct access grants enabled for that purpose. Whatever token you
end up with, put it in the `Authorization` header for subsequent requests.

---

### Endpoints

All routes are prefixed with `/api/v1`.

#### Customers

| Method | Path | Auth | Description |
|---|---|---|---|
| `POST` | `/customers` | No | Register (provisions the user in Keycloak too) |
| `GET` | `/customers` | Yes | List all (paginated) |
| `GET` | `/customers/{id}` | Yes | Get by ID |
| `GET` | `/customers/email/{email}` | Yes | Get by email |
| `PUT` | `/customers/{id}` | Yes | Update name / email |

> No account-deletion endpoint exists yet — see `docs/data-retention.md` for why
> that's a real gap if a right-to-erasure obligation applies, not just a
> documentation omission.

#### Accounts (under a customer)

| Method | Path | Auth | Description |
|---|---|---|---|
| `GET` | `/customers/{id}/accounts` | Yes | List accounts |
| `GET` | `/customers/{id}/accounts/{accountId}` | Yes | Get account |
| `POST` | `/customers/{id}/accounts` | Yes | Create account |
| `PATCH` | `/customers/{id}/accounts/{accountId}/deactivate` | Yes | Deactivate |

#### Transactions (under a customer)

| Method | Path | Auth | Description |
|---|---|---|---|
| `GET` | `/customers/{id}/transactions/filter` | Yes | Paginated + filtered list |
| `GET` | `/customers/{id}/transactions/summary` | Yes | Income / expenses / monthly breakdown |
| `GET` | `/customers/{id}/transactions/export` | Yes | Download as CSV |

#### Transactions (standalone)

| Method | Path | Auth | Description |
|---|---|---|---|
| `GET` | `/transactions/{id}` | Yes | Get by ID |
| `PATCH` | `/transactions/{id}/categorize` | Yes | Override category |

#### Webhooks

Transaction data is *received*, not polled — the account aggregator pushes new transactions to
us as they happen, rather than the API asking it for updates. There is no user-facing "sync"
button/endpoint.

| Method | Path | Auth | Description |
|---|---|---|---|
| `POST` | `/webhooks/bank-aggregator/transactions` | API key (`X-Api-Key` header) | Aggregator pushes new transactions for one linked account |

Auth is an API key, not a customer's JWT — this is a server-to-server call with no logged-in
user involved. Sources and their keys are **database rows, not config** — a `WebhookSource` per
source, managed live through the admin UI (`/admin/webhook-sources`, staff with the Keycloak
`admin` realm role only) or its backing API (`/api/v1/admin/webhook-sources`). Only a SHA-256
hash of each key is ever stored; the plaintext is shown exactly once, when it's created or
rotated, and can't be retrieved afterwards. Keys are per-source, not one shared secret: if one
source's key leaks, only that entry needs rotating (from the UI, no redeploy), and whichever key
a request presents tells the API which source it actually came from (logged as `SourceName` on
every ingest — see `WebhookApiKeyEndpointFilter`/`ReceiveBankTransactionsCommand`), not just "someone
with a valid key." The payload identifies the linked account by `externalAccountId`; the API
resolves it to the matching (active) `BankLink` to find which customer/internal account it
belongs to. **Each source is scoped to the institutions it serves** (`authorizedInstitutions`,
required when a source is created): a delivery is only ever applied to links at those
institutions, and one naming an account at any other institution is dead-lettered and audited
([ADR-0013](docs/adr/0013-webhook-source-institution-scoping.md)). The body is versioned
(`schemaVersion`, default 1); see [docs/event-contracts.md](docs/event-contracts.md) for the
full contract and its evolution rules:

```http
POST /api/v1/webhooks/bank-aggregator/transactions
X-Api-Key: <a key from /admin/webhook-sources>
Content-Type: application/json

{
  "schemaVersion": 1,
  "externalAccountId": "acc_123",
  "transactions": [
    { "id": "txn_abc", "amount": -150.00, "currency": "ZAR",
      "description": "Woolworths", "category": "Groceries", "date": "2026-09-10T12:00:00Z",
      "status": "posted" }
  ]
}
```

The response is `202 Accepted` with `{ "inboxMessageId", "isDuplicate", "requeued" }`.
Validation failures are `400` ProblemDetails with per-field messages under `errors`
(e.g. `{"errors": {"transactions[0].currency": ["Currency must be a 3-letter ISO 4217 code."]}}`),
and every response carries an `X-Correlation-Id` header (the caller's own when it is a short
`[A-Za-z0-9._-]` token, otherwise the trace id) that ties it to the logs.

#### Transaction status and totals

The bank decides when a transaction is settled. `status` is optional on each transaction:

| `status` | Stored as | Later |
|---|---|---|
| `posted`, or omitted | **Settled** | Replays are skipped as duplicates |
| `pending` | **Pending** | When the same `id` arrives again as `posted`, the existing row is **settled in place**, taking the bank's posted `amount` and `date`. This is recorded as `transaction.settled`, not as a duplicate. A late `pending` for an already-settled row is ignored |

**One rule for every money figure** (`TransactionTotals` in the domain): income, expenses,
net, spending by category and month, and account balances count **Settled** transactions
only. Pending authorisations are reported next to them, never inside them:
- `pendingIncome` / `pendingExpenses` on the summary and customer endpoints;
- `pendingBalance` / `availableBalance` on accounts. Available = balance less pending
  outflows; pending inflows aren't spendable yet.

Rejected, Cancelled, Refunded, Expired and the review states (Approved, Flagged, Disputed)
count toward neither.

**Stuck pending transactions expire.** A bank sometimes drops an authorisation and never
posts it. To stop such a row sitting in the pending figures and lowering the available
balance forever, a background job (`PendingExpiryBackgroundService`) runs every
`PendingExpiry:CheckIntervalMinutes` (default 60).
- It marks a transaction **Expired** once it has been Pending for longer than
  `PendingExpiry:ExpireAfterDays` (default 7). The clock starts from the later of the bank's
  transaction date and when we received it.
- Expired is our inference, not the bank's word, so a posting that arrives later still
  settles the row. A late *pending* for an expired row is ignored.
- Each expiry is audited as `transaction.expired` on the `system` channel, the affected
  customers' cached totals are cleared, and `pending_transactions_expired_total` counts expiries.
- It's safe with several replicas: an optimistic concurrency token on the row (Postgres
  `xmin`) makes a batch that races ingestion or another replica roll back rather than
  overwrite a posting.
- Set `PendingExpiry:Enabled=false` to turn it off.

#### Normalization

Every delivery is normalized with the linked bank's profile before anything else looks at
it — the bank link identifies the institution, so normalization runs right after the
customer is resolved. Rules live in `normalization-rules.json` (overridable per environment
like any configuration); `Default` applies to every bank, including ones with no profile,
and a bank's profile under `Institutions` only adds to it.

| Field | Normalized to |
|---|---|
| `date` | UTC. A date without an offset is read in the bank's time zone (default `Africa/Johannesburg`). |
| `description` | Trimmed, whitespace collapsed, known statement prefixes (e.g. `POS PURCHASE`) stripped |
| `currency` | Upper-case ISO 4217 |
| `category` | Mapped to our categories as a hint: our keyword rules win, then the bank's category, then income for money in |
| `id` | Never changed — duplicates are detected on it |

The bank's original description and category are kept on the transaction's metadata
(`bankDescription`, `bankCategory`), so cleaning never loses information.

#### Kafka (alternative inbound channel)

The same payload can be produced to the **`bank-transactions`** topic instead of calling the
webhook. The worker's consumer turns each record into the same `ReceiveBankTransactionsCommand` the
webhook uses, so both channels share validation, the inbox write, and idempotency. The record
value is the JSON body shown above. The key is optional (use `externalAccountId` to keep one
account's batches ordered on one partition).

- **Source:** set a **`source`** header to the name of a webhook source (created at
  `/api/v1/admin/webhook-sources`). It must exist and be active, otherwise the record is
  dead-lettered. Deactivating a source stops it on both channels. Kafka has no per-record
  credential, so use topic ACLs to stop one producer claiming another's name.
- A record **without** the header is recorded under `Kafka:SourceName` (default
  `kafka-bank-aggregator`). That name isn't checked against the registry. Set
  `Kafka:RequireSourceHeader=true` to dead-letter header-less records instead.

- **Delivery:** at-least-once. An offset is committed only after the record is stored in the
  inbox or moved to the dead-letter topic.
- **Transient failures** (e.g. Postgres down) retry the same record with exponential backoff (capped
  by `Kafka:MaxRetryBackoffSeconds`), which pauses that partition until it succeeds.
- **Records that can never succeed** (malformed JSON, failed validation, unknown source) go to
  **`bank-transactions.dlq`** with `dlq-reason` / `dlq-original-*` headers.
- Topics are created on startup (`Kafka:CreateTopics`). The consumer only runs when
  `ConnectionStrings:kafka` is set.

Browse and produce test records in Kafka UI (http://localhost:8083 in both docker-compose and
Aspire). From the host, the docker-compose broker is `localhost:9092`.

#### Joint accounts

A delivery is matched to customers by its `externalAccountId` through their active bank
links. A joint account has one link per holder, all with the same `externalAccountId`,
and **every holder receives the delivery**: each gets their own copy of each transaction,
under their own account, with their own duplicate detection and settlement. All holders'
copies commit together, so a retried delivery never finds one holder done and the other
not. A holder who links later receives deliveries from then on; earlier history isn't
backfilled.

#### Idempotency and duplicate notifications

Duplicates are checked at two levels. Neither level fails the rest of a delivery:

| Level | Detected by | What happens |
|---|---|---|
| **Delivery** (same webhook call / Kafka record sent again) | `Idempotency-Key` HTTP header or `idempotency-key` Kafka header. Without one, the SHA-256 of the payload is used. Unique per source. | No second inbox row. The caller gets the original `inboxMessageId` with `isDuplicate: true`. If that original was dead-lettered, it's requeued with a fresh retry budget (`requeued: true`). The same key with a **different** payload is refused with `422` (`Inbox.IdempotencyKeyReused`; dead-lettered on Kafka), never acknowledged as a duplicate, because that would drop the new data. |
| **Transaction** (external id already stored for the customer at that institution, or repeated inside one batch) | Unique index on `(CustomerId, SourceName, SourceExternalId)` plus a lookup before insert. External ids are only unique per bank, so the same id from two banks is two transactions. | Only the duplicates are skipped; every other transaction in the batch is stored. A concurrent insert that wins the race triggers a re-check and retry, not a dropped batch. |

Every duplicate at either level:

- increments `inbound_duplicates_total{source_name, level}`
- writes a `DuplicateInboundDetected` outbox message. The outbox dispatcher logs it and, if
  `NotificationOptions:DuplicateAlertWebhookUrl` is set, POSTs an alert there. Because this is
  asynchronous and never throws, a failing alert channel can't block ingestion.

#### Audit trail (admin)

Every inbound delivery leaves an append-only trail in `audit."AuditEvents"` (the
Audit module, see [ADR-0011](docs/adr/0011-audit-trail-for-inbound-data.md)). It records
where the data came from (channel and source), when, how (the channel's own metadata),
and what happened to it afterwards.

| Method | Path | Description |
|---|---|---|
| `GET` | `/admin/audit/events` | Search, newest first. Filters: `channel`, `sourceName`, `eventType`, `externalAccountId`, `inboxMessageId`, `customerId`, `transactionId`, `externalTransactionId`, `from`, `to`, `pageNumber`, `pageSize` (max 200) |
| `GET` | `/admin/audit/transactions/{id}/lineage` | One transaction's origin: channel, source, received/ingested times, and every event of the delivery that carried it |

Requires the Keycloak `admin` realm role. There is no endpoint to edit or delete audit
history, and a database trigger rejects UPDATE/DELETE/TRUNCATE on the table.

The admin UI has an **Audit Trail** page (`/admin/audit`, in the sidebar for admins). It
offers filters and 1h/24h/7d presets, and each row expands to show its full metadata. A
**delivery** link shows every event of one webhook call or Kafka record. An **origin** link,
or the *Trace origin* box, shows one transaction's lineage as a timeline. Filters are kept in
the URL, so any view can be shared as a link.

| Event type | When | Channel metadata |
|---|---|---|
| `inbound.received` | Delivery stored in the inbox | Webhook: `remoteIp`, `userAgent`, `requestId`. Kafka: `topic`, `partition`, `offset`, `key`, `producedAt`, `consumerGroup`. Always: `payloadSha256`, `transactionCount`, `idempotencyKeySource` |
| `inbound.duplicate` / `inbound.requeued` | Replay of a stored / dead-lettered delivery | As above, plus `originalStatus` |
| `inbound.rejected` | Failed validation, or malformed Kafka record (moved to the DLQ) | As above; reason in `detail` |
| `inbound.unauthorized` | Webhook call with a missing or unknown API key (the key is never recorded) | Webhook metadata |
| `inbound.processed` / `inbound.processing_failed` / `inbound.dead_lettered` | Inbox dispatcher outcome | `ingestedCount`, `duplicateCount` / `attempt`, `maxAttempts`, `nextAttemptAt` |
| `transaction.ingested` / `transaction.duplicate_skipped` | Per transaction in a delivery | `bankLinkId`, `accountId`, `institution`, `amount`, `currency`, `status` |
| `transaction.settled` | A pending (or expired) transaction the bank has now posted | `amount`, `previousStatus`, plus `pendingAmount` / `pendingDate` when the posting changed them |
| `transaction.expired` | Pending past `PendingExpiry:ExpireAfterDays` with no posting (channel `system`) | `amount`, `currency`, `institution`, `pendingSince`, `cutoff` |

Events that describe a database change are queued through the transactional outbox in
the same commit as that change, so they appear within one outbox poll (~5 s) and are
never recorded for a change that rolled back. All events of one delivery share its
`inboxMessageId`; `traceId` links to the request's logs and traces in Seq.

#### Admin — webhook sources

Manages the `WebhookSource` rows above (who's allowed to call the webhook and with what key).
Requires the Keycloak `admin` realm role — a customer JWT without it gets `403`.

| Method | Path | Description |
|---|---|---|
| `GET` | `/admin/webhook-sources` | List sources (never includes key material) |
| `POST` | `/admin/webhook-sources` | Create a source: `{ "name", "authorizedInstitutions": ["FNB", "Absa"] }`. The response includes the API key once |
| `PUT` | `/admin/webhook-sources/{id}/institutions` | Replace the institutions a source may deliver for: `{ "authorizedInstitutions": [...] }` |
| `POST` | `/admin/webhook-sources/{id}/rotate` | Replace the key — old one stops working immediately |
| `POST` | `/admin/webhook-sources/{id}/activate` | Re-enable a deactivated source |
| `POST` | `/admin/webhook-sources/{id}/deactivate` | Disable without deleting (key stops authenticating) |

#### Health & Metrics

| Method | Path | Auth | Description |
|---|---|---|---|
| `GET` | `/health` | No | Readiness — checks Postgres + Redis (Redis reports Degraded, not Unhealthy) |
| `GET` | `/alive` | No | Liveness — self only (fast) |
| `GET` | `/metrics` | No | Prometheus scrape endpoint |

---

### Filter query parameters

`GET /customers/{id}/transactions/filter` accepts:

| Parameter | Type | Description |
|---|---|---|
| `pageNumber` | int | Default `1` |
| `pageSize` | int | Default `20`, max `100` |
| `fromDate` | datetime | Transaction date lower bound |
| `toDate` | datetime | Transaction date upper bound |
| `category` | int | `0`=Uncategorized `1`=Groceries `2`=Dining `3`=Transportation `4`=Entertainment `5`=Utilities `6`=Housing `7`=Healthcare `8`=Income `9`=Transfer `10`=Shopping `11`=Subscriptions |
| `status` | int | `0`=Pending `1`=Approved `2`=Rejected `3`=Flagged `4`=Settled `5`=Refunded `6`=Disputed `7`=Cancelled |
| `minAmount` | decimal | Absolute amount lower bound |
| `maxAmount` | decimal | Absolute amount upper bound |
| `searchTerm` | string | Matches description or source name |
| `source` | string | Institution the transaction came from, e.g. `FNB`, `Absa`, `Capitec`, `StandardBank` |
| `sortBy` | string | `date` / `amount` / `category` / `status` / `description` |
| `sortDescending` | bool | Default `true` |

---

### Rate limits

| Scope | Limit | Notes |
|---|---|---|
| Per endpoint group | 60 req / min per client | `/customers`, `/transactions`, `/accounts` |
| Global (burst ceiling) | 100 req / min per client | All routes combined |

Returns `HTTP 429` with a `Retry-After` header when exceeded.
Client identity: authenticated username → remote IP → `anonymous`.
Counters live in Redis and are shared across all replicas.

---

## Configuration reference

Environment variables use `__` as a section separator:
`ConnectionStrings__transactiondb` → `ConnectionStrings:transactiondb`.

| Key | Description | Default (Docker Compose) |
|---|---|---|
| `ConnectionStrings:transactiondb` | PostgreSQL connection string | `Host=postgres;Port=5432;Database=transactiondb;Username=postgres;Password=postgres` |
| `ConnectionStrings:redis` | Redis connection string | `redis:6379` |
| `ConnectionStrings:seq` | Seq ingestion URL | `http://seq:80` |
| `Keycloak:Authority` | Keycloak server root the API itself calls (in-cluster/compose service address) | `http://keycloak:8080` |
| `Keycloak:PublicIssuer` | Full realm URL the browser uses — must match the `iss` claim on tokens | `http://localhost:8081/realms/transaction-aggregation` |
| `Keycloak:Realm` | Keycloak realm name | `transaction-aggregation` |
| `Keycloak:Audience` | Expected token audience | `transaction-ui` |
| `Keycloak:AdminClientId` / `AdminClientSecret` | Confidential service-account client used to provision customers | Set in `appsettings.Development.json` / k8s Secret |
| `ASPNETCORE_ENVIRONMENT` | `Development` / `Production` | `Development` |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | OTLP collector for traces (optional) | not set |

---

## Troubleshooting

### Docker Compose — API restarts on first start

PostgreSQL takes a few seconds to initialise. The API retries automatically.
If it keeps restarting:

```bash
docker-compose restart api
```

### Docker Compose — Grafana shows no data

Confirm the API is reachable from Prometheus:

```bash
# Check Prometheus targets — all should show State=UP
open http://localhost:9090/targets
```

If `transaction-api` is down, confirm the API container is healthy:

```bash
docker-compose ps
curl http://localhost:5001/metrics
```

### Aspire — UI keeps loading / spinner never goes away

This is normal on the first debug build — the unoptimised Blazor WASM bundle
is large (~50–100 MB). Subsequent loads hit the browser cache and are fast.
If it never loads at all, check the browser console for JavaScript errors.

### Kubernetes — pods stuck in `Pending`

The PVC cannot bind to a storage class. Check:

```bash
kubectl describe pvc -n transaction-aggregation
kubectl get storageclass
# must show "local-path" (Rancher Desktop default)
```

### Kubernetes — images not found (`ErrImageNeverPull`)

The images were not built into the k3s containerd namespace:

```bash
nerdctl --namespace k8s.io images | grep transaction
```

If missing, re-run the `nerdctl build` commands from Step 1. Also confirm
Rancher Desktop is using **containerd** (Preferences → Container Engine).

### Kubernetes — API pods not becoming Ready

```bash
kubectl logs      deployment/transaction-api -n transaction-aggregation
kubectl describe  pod -n transaction-aggregation \
  -l app.kubernetes.io/name=transaction-api
```

The readiness probe calls `/health` (checks Postgres + Redis). If either
dependency is unhealthy the pod waits until they recover.

Migrations run in the `db-migrate` Job before the API rolls out, not inside API pods.
If the deploy script stopped at "Database migrations", read the Job's logs:

```bash
kubectl logs job/db-migrate -n transaction-aggregation
```

### Kubernetes — Ingress returns 404

```bash
kubectl get ingress -n transaction-aggregation
# ADDRESS column shows the Rancher Desktop VM IP (192.168.127.2) — that is normal.
# Services are accessible via 127.0.0.1 on the host through Rancher Desktop's port forwarding.
```

If ADDRESS is blank, Traefik is still picking up the Ingress — wait a few
seconds. Bypass and test directly:

```bash
kubectl port-forward svc/transaction-api 8080:80 -n transaction-aggregation
curl http://localhost:8080/health
```

### Kubernetes — hostnames not resolving on Windows (Rancher Desktop)

The deploy script updates the WSL2 `/etc/hosts` file. **Windows browsers read a
separate file** — `C:\Windows\System32\drivers\etc\hosts` — and will time out
unless the hostnames are added there too.

Open **Notepad as Administrator** (right-click → Run as administrator), open
`C:\Windows\System32\drivers\etc\hosts`, and add whichever lines you need:

```
127.0.0.1  api.transaction.local
127.0.0.1  ui.transaction.local
127.0.0.1  prometheus.transaction.local
127.0.0.1  grafana.transaction.local
127.0.0.1  pgadmin.transaction.local
127.0.0.1  redis-commander.transaction.local
```

Use `127.0.0.1` — Rancher Desktop forwards port 80 from the Windows loopback
to the Traefik ingress controller running inside the VM.

To confirm Traefik is reachable before editing the hosts file:

```bash
# From WSL2 — should return {"database":"ok",...}
curl http://127.0.0.1/api/health -H "Host: grafana.transaction.local"
```

### Kubernetes — UI loads but API calls fail

The nginx UI pod proxies `/api/` to `transaction-api`. Check nginx logs:

```bash
kubectl logs deployment/transaction-ui -n transaction-aggregation
```

Causes and fixes:

| Cause | Fix |
|---|---|
| UI ConfigMap not applied | `kubectl apply -f k8s/ui/configmap.yaml` |
| API pods not ready | `kubectl get pods -n transaction-aggregation` |
| NetworkPolicy blocking traffic | Confirm `allow-ui-to-api` policy is applied |

### Kubernetes — Grafana shows no data

1. Check Prometheus targets at `http://prometheus.transaction.local/targets`
   (or via port-forward on port 9090).
2. Confirm the API pod has the annotation `prometheus.io/scrape: "true"` —
   it is set in `k8s/api/deployment.yaml`.
3. Verify `/metrics` returns data:
   ```bash
   curl http://api.transaction.local/metrics | head -20
   ```
4. In Grafana, confirm the Prometheus datasource URL is `http://prometheus:9090`
   (Settings → Data Sources → Prometheus → Test).

### JWT 401 Unauthorized

Check the `Authorization` header is exactly:

```
Authorization: Bearer eyJhbGciOiJI...
```

Log in again through the UI to get a fresh token if it expired. If every request 401s
regardless of the token, the API most likely can't validate against Keycloak — check:
- `Keycloak:Authority` (or `Keycloak__Authority` in k8s/compose) actually resolves from inside
  the API's container/pod (it's the in-cluster/compose address, not the browser-facing one).
- `Keycloak:PublicIssuer` exactly matches the token's `iss` claim — decode a token at
  jwt.io and compare, or check Keycloak's `KC_HOSTNAME` setting matches what you configured.
- The realm actually imported — `http://localhost:8081/realms/transaction-aggregation` (or the
  k8s ingress equivalent) should return realm metadata, not a 404.

### Rate limit 429 on every request

Limits: 60 req/min per endpoint group, 100 req/min global.
To raise temporarily for testing:

- Global → `Program.cs` `AddOptions<RateLimiterOptions>` block: `PermitLimit = 3000`
- Endpoint policy → `RateLimiting/RedisFixedWindowPolicy.cs`: `PermitLimit = 600`

Rebuild and redeploy. Revert before committing.
