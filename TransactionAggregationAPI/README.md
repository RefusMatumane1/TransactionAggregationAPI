# Transaction Aggregation API

The detailed guide to the Transaction Aggregation API: how to run it, what is in the repository,
and the full API and configuration reference. For an overview of what the system does and why it
is built this way, start with the [root README](../README.md).

A .NET 10 system that records the posted transactions banks push to it (each bank is a source with
its own API key) in an insert-only ledger, categorises them automatically, and exposes aggregate
views (by customer, bank, account, category and period, in one currency at a time) through a
versioned REST API. Nothing in the ledger is ever edited or deleted.

A customer is the person whose money it is: administrators link each customer's bank accounts,
across banks, and every view can be narrowed to one customer. Customers don't sign in; the people
who do are staff, who see the banks they are assigned, and administrators.

It comes with a Blazor WASM frontend and three ways to run it locally: Docker Compose, .NET Aspire
and Kubernetes manifests.

---

## Table of contents

1. [What this project does](#what-this-project-does)
2. [Tech stack](#tech-stack)
3. [Project structure](#project-structure)
4. [Seed data and logins](#seed-data)
   - [Mock bank feeds (development)](#mock-bank-feeds-development)
5. [Option A — Docker Compose](#option-a--docker-compose-quickest)
6. [Option B — .NET Aspire (debug)](#option-b--net-aspire-debug)
7. [Option C — Kubernetes](#option-c--kubernetes-rancher-desktop)
8. [API reference](#api-reference)
9. [Configuration reference](#configuration-reference)
10. [Troubleshooting](#troubleshooting)

---

## What this project does

- Records posted transactions from multiple banks (mock FNB, Absa, Capitec and Standard Bank feeds in development), received by REST webhook or signed Kafka records, with idempotent ingestion, in an **insert-only ledger** enforced by the database
- Publishes a `TransactionRecorded` integration event to Kafka for every ledger entry
- Categorises transactions automatically by keyword (Groceries, Dining, Transport …)
- Aggregates by bank, account, category and period (cash flow, breakdowns and period-over-period comparison) in any ISO 4217 currency, one currency per view
- Admins manage the banks (each one a source with its own API key, display name and colour) from the UI
- Exposes a versioned REST API (`/api/v1/…`) secured with Keycloak-issued JWT bearer tokens; users hold the `staff` (read-only, limited to their assigned banks) or `admin` realm role
- Audits every delivery, every ledger entry and every admin change (with the admin who made it) in an append-only trail
- Keeps secrets out of git: an untracked `secrets.json` locally, the same file rendered by Vault in clusters
- Caches query results in Redis to reduce database round-trips
- Enforces distributed rate limiting across all replicas via Redis
- Emits structured logs to Seq and traces via OpenTelemetry
- Exposes a Prometheus `/metrics` scrape endpoint via prometheus-net
- Ships with Grafana dashboards pre-provisioned with HTTP, runtime, and GC panels
- Ships with a Blazor WASM frontend served by nginx in production
- Applies EF Core migrations on startup in Development; elsewhere a gated migration Job applies them before the API rolls out

---

## Tech stack

| Layer | Technology |
|---|---|
| Runtime | .NET 10 / ASP.NET Core Minimal API |
| Frontend | Blazor WebAssembly (.NET 10) |
| Database | PostgreSQL 17.6 + Entity Framework Core 10 |
| Cache / Rate limiting | Redis 7 + StackExchange.Redis |
| Messaging | Kafka (Confluent.Kafka): inbound bank records, outbound integration events |
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
│   ├── Development/SeedData.cs         ← 21 demo accounts at four banks, ~2 700 ledger entries
│   ├── Program.cs                      ← App bootstrap
│   ├── appsettings.json                ← Non-secret settings (tracked)
│   ├── secrets.template.json           ← Shape of the secrets (tracked)
│   └── secrets.json                    ← Your local secrets (git-ignored; Vault renders it in clusters)
│
├── TransactionAggregationUI/           ← Blazor WASM frontend
│   ├── Pages/                          ← Dashboard (aggregates), Customers (cross-bank view, account linking), Transactions, Admin (webhook sources, audit trail) …
│   ├── Services/                       ← HTTP clients for each API resource
│   ├── Auth/                           ← Claims helper for the OIDC-issued principal
│   ├── wwwroot/appsettings.json        ← ApiBaseUrl, Keycloak:Authority (templated at container start)
│   ├── nginx.conf                      ← Proxies /api/ to the API service
│   └── Dockerfile                      ← nginx + published WASM static files
│
├── TransactionAggregation.MockAggregator/ ← Development only: stand-in account aggregator — mock FNB/Absa/Capitec/Standard Bank
│                                         feeds for a fixed set of accounts, pushed through the real webhook or Kafka
├── TransactionAggregation.Worker/      ← All background processing, and only here: Kafka consumer, inbox/outbox dispatchers,
│                                         integration-event publishing, message archiving, duplicate alerts (the API runs none of it)
│   └── Dockerfile                      ← Scales independently of the API (serves only /liveness, /readiness, /health; /metrics on :9464)
│
├── TransactionAggregation.Hosting/     ← Composition shared by the API and the worker (modules, Postgres, Redis cache,
│                                         Data Protection key ring, Serilog, categorization-rules.json)
│
├── TransactionAggregationAPI.AppHost/  ← .NET Aspire orchestration
│   ├── AppHost.cs                      ← Wires up API + worker + Postgres + Redis + Seq + Kafka
│   └── secrets.json                    ← Local parameter values (git-ignored; copy secrets.template.json)
│
├── Modules/Transactions/Transactions.Application/    ← Transaction use cases (CQRS / MediatR), categorization
├── Modules/Transactions/Transactions.Domain/         ← Immutable Transaction (ledger entry), Money, TransactionSource
├── Modules/Transactions/Transactions.Contracts/      ← TransactionRecorded v1 integration event (dependency-free)
├── Modules/Transactions/Transactions.Infrastructure/ ← TransactionsDbContext (`transactions` schema), migrations
├── TransactionAggregationAPI.ServiceDefaults/ ← Health checks, OpenTelemetry, prometheus-net
│
├── BuildingBlocks/                     ← Shared technical infrastructure; depends on no module
│   ├── SharedKernel/                   ← Domain primitives only (BaseEntity, domain events, ValueObject, Result/Error) — no persistence/validation/logging framework
│   ├── BuildingBlocks.Application/     ← ICommand/IQuery, IUserContext, the shared MediatR pipeline (validation, logging, caching) and module registration
│   ├── BuildingBlocks.Persistence/     ← AppDbContextBase, module DbContext registration (retry policy, migrations assembly), design-time support
│   ├── BuildingBlocks.Messaging/       ← Generic Inbox/Outbox reliability mechanism — own DbContext, `messaging` schema
│   └── BuildingBlocks.Web/             ← HTTP helpers every module's Presentation reuses (Result → ProblemDetails, versioned route groups,
│                                         shared policy and role names)
├── Modules/                            ← Each module: Domain · Application · Infrastructure · Presentation (+ Contracts for other modules)
│   │                                     Presentation = the module's HTTP surface: one file per endpoint under Endpoints/, wire shapes under
│   │                                     Requests/ and Responses/. Application DTOs are always mapped to a response, never serialized
│   │                                     directly. Referenced only by the API host — the worker runs every module without it.
│   ├── WebhookSources/                 ← First fully-extracted module — own DbContext, `webhooksources` schema, depends only on SharedKernel
│   └── Audit/                          ← Append-only inbound audit trail — own DbContext, `audit` schema, depends on no other module
│
├── monitoring/                         ← Docker Compose monitoring stack
│   ├── prometheus.yml                  ← Prometheus scrape config (api:9464 and every worker replica's /metrics)
│   ├── prometheus-rules.yml            ← Alert rules (compose + Aspire; k8s keeps the same groups inline)
│   └── grafana/
│       ├── generate_dashboards.py      ← Dashboards as code — writes dashboards/*.json and the k8s ConfigMap
│       ├── provisioning/               ← Auto-provisioned datasource + dashboard provider
│       └── dashboards/                 ← Overview, Transaction API, Ingestion Pipeline
│
├── docker-compose.yml                  ← Full local stack including Prometheus + Grafana
├── docker-compose.override.yml         ← Dev overrides (ports)
├── .env.template                       ← Compose credentials to fill in; copy to .env (git-ignored)
├── deploy-k8s.sh                       ← One-command Kubernetes deployment script
│
└── k8s/                                ← Kubernetes manifests
    ├── namespace.yaml
    ├── secrets.yaml                    ← Fill in before applying (or use --vault)
    ├── configmap.yaml                  ← API environment variables
    ├── network-policy.yaml             ← Pod-level traffic rules
    ├── postgres/ redis/ seq/ kafka/    ← StatefulSets + Services (persistent volumes)
    ├── pgbouncer/                      ← Connection pooling between the pods and PostgreSQL
    ├── keycloak/                       ← Realm import, Deployment, PVC, Ingress
    ├── api/                            ← Deployment, Service, Ingress, HPA, PDB, migration Job
    ├── worker/                         ← Deployment, PDB, KEDA ScaledObject / CPU HPA
    ├── ui/                             ← Deployment, Service, Ingress, ConfigMap
    ├── dev-tools/                      ← pgAdmin, Redis Commander, Kafka UI (optional --dev-tools)
    ├── monitoring/                     ← Prometheus + Grafana (optional --monitoring)
    │   ├── prometheus/                 ← ServiceAccount, RBAC, ConfigMap, Deployment, Service, Ingress
    │   └── grafana/                    ← ConfigMaps (provisioning + dashboards), PVC, Deployment, Service, Ingress
    └── vault/                          ← Vault Agent patches and policy (--vault)
```

---

## Seed data

Every Development start seeds the database (`Development/SeedData.cs`), each demo account under
its own bank:

- **21 demo accounts** (checking, savings, credit card, investment) at FNB, Standard Bank, Absa
  and Capitec, grouped into ten households with their own salary, rent and spending patterns.
  An account is only an id (e.g. `ZA0010000001`); there are no owners.
- An account with no transactions gets **15 months of history ending today**, so every
  "last 12 months" view has data.
- An account that already has data is **topped up** from its latest transaction to now, plus a
  burst of recent activity, so each restart visibly adds new transactions.
- Each run uses a **new random seed**, so amounts, descriptions and dates differ
  every time. The seed and run id are logged; set `Seed__RandomSeed` to reproduce a run.

### Mock bank feeds (development)

The seed data above is written straight into the database. To exercise the **real**
pipeline — ingestion, normalization, categorization, aggregation —
`TransactionAggregation.MockAggregator` stands in for the external account aggregator in
Docker Compose and Aspire (never in Kubernetes). Every 30 s it pushes new transactions for
each account in its catalog (`Catalog/MockCatalog.cs`) through the API's webhook
(`Feed__Channel=Kafka` sends to the topic instead), naming the institution in every delivery.
Some card purchases arrive `pending` and post on a later tick (restaurants with a tip added).
The pending notice is audited and skipped, and only the posting is recorded. Now and then a
whole batch is re-sent, to exercise duplicate detection.

Each mock bank writes transactions its own way. **These formats are invented for the mock**;
their normalization rules live in `normalization-rules.Development.json`, which only
Development loads:

| Mock bank | Description as sent | Date as sent | Category as sent |
|---|---|---|---|
| FNB | `POS PURCHASE  WOOLWORTHS  SANDTON` | local time, no offset | its own labels (`Takeaways`, `Petrol`, …) |
| Absa | `ABSA CARD Woolworths Sandton` | `+02:00` offset | none |
| Capitec | `Woolworths Sandton` | UTC | our category names |
| Standard Bank | `PURCHASE Woolworths` | local time, no offset | none — no profile, so only the default rules apply ("Other") |

**Watch transactions arrive:** sign in as `staff@test.com` and open the **Dashboard**. Each
bank's card grows every feed interval; click a bank (or an account) to open **Transactions**
filtered to it, where each delivery lands.

The two apps share a base API key (`MockAggregator:WebhookApiKey` and `Feed:ApiKey` in their
`secrets.json`, the AppHost's `mock-aggregator-api-key` parameter, or `MOCK_AGGREGATOR_API_KEY`
in `.env`). In Development the API registers each mock bank
(FNB, StandardBank, Absa, Capitec) as its own source at startup, keyed `<base>-<code in lower
case>`, and the mock sends each bank's feed with that bank's key.

### Logins

Everyone who signs in is staff or an administrator; there is no self-registration. The realm
import (`keycloak/realm-export.json`) creates one of each for local use:

| User | Realm role | Can | Password |
|---|---|---|---|
| `staff@test.com` | `staff` | Dashboard, customers, aggregates and transaction list for FNB and StandardBank (its `institutions` attribute) | `TRANSACTION_APP_STAFF_PASSWORD` in `.env` (compose) or `app-staff-password` in the AppHost's `secrets.json` (Aspire) |
| `admin@test.com` | `admin` | Every bank, plus managing the banks and the audit trail | `TRANSACTION_APP_ADMIN_PASSWORD` / `app-admin-password` |

No password is committed. Keycloak substitutes them into the realm when it first imports it into
an empty volume, so after changing one, recreate the Keycloak volume (`docker compose down -v`, or
delete `transaction-keycloak-data` under Aspire).

In Kubernetes (`k8s/keycloak/configmap.yaml`) only the admin user is imported, with the
`app-admin-password` key of `transaction-api-secrets` (never committed; `deploy-k8s.sh` refuses
to deploy while it is still the placeholder). Create staff users in the Keycloak admin console,
give them the `staff` role, and list their banks in the `institutions` attribute (bank codes, for
example `FNB`). A staff user with no banks sees nothing. A signed-in user with neither role gets a
"No access" page.

---

## Option A — Docker Compose (quickest)

One command starts the system: API, worker, UI, PostgreSQL, Redis, Kafka, Keycloak, Seq and
the mock bank aggregator. Migrations and seeding run automatically on first start. Tooling is
opt-in through profiles: `--profile tools` (pgAdmin, Redis Commander, Kafka UI) and
`--profile monitoring` (Prometheus, Grafana).

### Prerequisites

- [Docker Desktop](https://www.docker.com/products/docker-desktop) running

### Run

```bash
cd /path/to/TransactionAggregationAPI

cp .env.template .env        # once: fill in every value (compose refuses to start without them)
docker compose up --build                                        # the system
docker compose --profile tools --profile monitoring up --build   # plus tooling

scripts/smoke-test.sh http://localhost:5001                      # verify it
```

### What's running

| Service | URL |
|---|---|
| **UI** (Blazor frontend) | http://localhost:7200 |
| **API** | http://localhost:5001 |
| **API docs** (Scalar) | http://localhost:5001/scalar/v1 |
| **Seq** structured logs | http://localhost:5341 |
| **Prometheus** (`--profile monitoring`) | http://localhost:9090 |
| **Grafana** (`--profile monitoring`) | http://localhost:3000 (admin / `GRAFANA_ADMIN_PASSWORD`) |
| **pgAdmin** (`--profile tools`) | http://localhost:5050 |
| **Redis Commander** (`--profile tools`) | http://localhost:8082 |
| **Kafka UI** (`--profile tools`) | http://localhost:8083 |
| **Kafka broker** (from the host) | `localhost:9092` |
| **Mock aggregator** (bank feeds) | http://localhost:5090 |

> **pgAdmin first-time setup:** log in as `admin@test.com` / `PGADMIN_PASSWORD`, then
> add server → host `postgres`, port `5432`, user `postgres`, password `POSTGRES_PASSWORD`.

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

# once: copy each secrets.template.json to secrets.json and fill it in
#   TransactionAggregationAPI.AppHost/, TransactionAggregationAPI/, TransactionAggregation.Worker/,
#   TransactionAggregation.MockAggregator/   (all git-ignored)
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
| **Mock aggregator** (bank feeds) | http://localhost:5090 |
| **Prometheus** (targets, alerts) | http://localhost:9090 |
| **Grafana** | http://localhost:3000 (admin / `grafana-admin-password`) |

> Aspire reads its parameters (PostgreSQL, Keycloak, Grafana and dev-user passwords, the mock's
> base key) from the git-ignored `TransactionAggregationAPI.AppHost/secrets.json`. Keep the
> PostgreSQL password stable: the data volume keeps the password it was created with.

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
| **Worker** (.NET 10) | 2 | Kafka consumer, inbox/outbox dispatchers, event publishing; KEDA on backlog (2–30) or CPU HPA |
| **db-migrate Job** | 1 run | Applies migrations as `tagg_migrator` before every rollout |
| **PgBouncer** | 2 | Transaction pooling between the pods and PostgreSQL |
| **PostgreSQL 17.6** | 1 | StatefulSet + 10 Gi PVC |
| **Redis 7** | 1 | StatefulSet + 2 Gi PVC, AOF persistence |
| **Seq** | 1 | StatefulSet + 5 Gi PVC; access via port-forward (no Ingress) |
| **Kafka** (KRaft, single node) | 1 | StatefulSet + 5 Gi PVC; `bank-transactions` (6 partitions) + `.dlq`, and `transaction-events` |
| **Keycloak** | 1 | Realm import with the admin user; staff users are created in the console |
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

### Step 2 — Provide the secrets

**With Vault** (production-like): pass `--vault`. The Vault Agent Injector renders each pod's
`secrets.json` (the same shape developers keep locally) and the hosts read it through
`Secrets:FilePath`. Setup, policy and paths are in [k8s/vault/README.md](k8s/vault/README.md).

**Without Vault** (a local cluster): replace every `CHANGE_ME_BEFORE_DEPLOY` value in
`k8s/secrets.yaml` (`postgres-password`, `keycloak-admin-password`, `app-admin-password`,
`migrator-db-password`, `app-db-password`, `pgadmin-password`), for example with
`openssl rand -base64 24 | tr -d '\n' | base64`. `deploy-k8s.sh` refuses to deploy while any
placeholder remains.

> **Never commit `secrets.yaml` with real values.**
> Protect it: `git update-index --assume-unchanged k8s/secrets.yaml`

> **Upgrading an existing cluster from PostgreSQL 16:** the StatefulSet now runs 17.6, which
> cannot open a 16 data directory. Dump first (`pg_dump -Fc`), redeploy on a fresh volume, then
> restore with `pg_restore`, or `--teardown` a disposable cluster.

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

# Read the API and worker secrets from Vault
./deploy-k8s.sh --vault

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
| Secrets & ConfigMaps | Applies secrets and the API and worker ConfigMaps; UI ConfigMap *(if `--ui`)* |
| Data stores | Deploys PostgreSQL, Redis, Seq, Kafka and Keycloak, and waits for them |
| Migrations | Runs the `db-migrate` Job and stops the deployment if it fails |
| PgBouncer | Deploys the connection pooler |
| API | Deploys Service, Deployment, Ingress, HPA, PDB (Vault patch with `--vault`) and waits for `/readiness` |
| Worker | Deploys the worker, PDB and autoscaler (KEDA when available, otherwise CPU HPA) |
| UI *(if `--ui`)* | Deploys Deployment, Service, Ingress — waits for readiness |
| Network policies | Applies pod-level traffic rules |
| Dev tools *(if `--dev-tools`)* | Deploys pgAdmin and Redis Commander |
| Monitoring *(if `--monitoring`)* | Applies Prometheus RBAC then Deployment + Service + Ingress; Grafana ConfigMaps, PVC, Deployment, Service, Ingress |
| /etc/hosts | Adds hostnames for all deployed services via sudo (skips entries already present) |
| Smoke test | Runs `scripts/smoke-test.sh` against the deployed API |

> Pods never migrate outside Development. The `db-migrate` Job applies every module's migrations
> with the same image the API is about to run, as the schema owner, before any replica starts.

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
validates the JWTs Keycloak signs. Every endpoint except the webhook and health checks requires a
bearer token carrying the `staff` or `admin` realm role (see [Logins](#logins)):

```
Authorization: Bearer <token>
```

Users are created in Keycloak (admin console → Users) and given a role there; the API has no
registration endpoint.

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

#### Transactions

Readable by `staff` and `admin`, and read-only. The ledger is insert-only: no endpoint changes or
deletes a transaction, its category included, and the database refuses such changes from anyone.
Staff see only the banks in their `institutions` claim; another bank's transaction is a `404`.
Every read can be narrowed with `institution` (a bank code) and `externalAccountId` (which
requires `institution`).

| Method | Path | Role | Description |
|---|---|---|---|
| `GET` | `/transactions` | staff | Cursor-paginated, filtered, sorted list (see [Filter query parameters](#filter-query-parameters)) |
| `GET` | `/transactions/{id}` | staff | Get by ID, with its bank, account and the bank's metadata |
| `GET` | `/transactions/summary` | staff | Income / expenses / spend per category / monthly breakdown |
| `GET` | `/transactions/aggregates/institutions` | staff | Totals and account count per bank, then per account |
| `GET` | `/banks` | staff | The bank directory: code, display name, colour, active, last delivery |
| `GET` | `/transactions/aggregates/categories` | staff | Spending or income per category, with share |
| `GET` | `/transactions/aggregates/cash-flow` | staff | Income / expenses / net per day, week or month |
| `GET` | `/transactions/aggregates/comparison` | staff | Period against the previous equal period |

Aggregate periods are South African calendar days (`from`, `to`; default the last 12 months).
Every aggregate is computed in one currency, `currency` (ISO 4217, default `ZAR`), so amounts in
different currencies are never added together.

Aggregates are never computed on the request path. The worker rebuilds a daily read model
(`transactions."DailyTotals"`: one row per day, bank, account, category and currency) every
`Aggregation:IntervalMinutes` (60 by default). Each rebuild recomputes, in full, every account-day
that received entries since the last one, so a backdated or re-delivered transaction can't be
double counted. Every aggregate response carries `asOf`, the time of the last rebuild; a
transaction recorded since then is in the transaction list but not yet in the totals.
`/transactions/summary` reads the same model, so its `startDate`/`endDate` are rounded to the South
African days they fall on (both inclusive).

#### Customers

A customer groups bank accounts across banks: a link is the same (bank, account id) pair every
transaction carries, so linking an account brings in its past and future transactions, and
unlinking removes them from the customer's view without touching the ledger. Links are resolved
on every read. Staff see a customer only through accounts at their banks, and only those
accounts; a customer with none of them is a `404`, exactly like a missing one.

| Method | Path | Role | Description |
|---|---|---|---|
| `GET` | `/customers` | staff | Customers the caller can see, by name, cursor-paginated; `search` matches name or reference |
| `GET` | `/customers/{id}` | staff | The customer and the linked accounts the caller may read |
| `GET` | `/customers/{id}/transactions` | staff | The customer's transactions across banks; same filters and paging as `/transactions` |
| `GET` | `/customers/{id}/aggregates/cash-flow` | staff | As `/transactions/aggregates/cash-flow`, for the customer |
| `GET` | `/customers/{id}/aggregates/categories` | staff | As `/transactions/aggregates/categories`, for the customer |
| `GET` | `/customers/{id}/aggregates/institutions` | staff | The customer's totals per bank, then per account |
| `GET` | `/customers/{id}/aggregates/comparison` | staff | As `/transactions/aggregates/comparison`, for the customer |
| `POST` | `/customers` | admin | Register a customer: `{ "reference": "CUST-0001", "name": "Thandi Nkosi" }` → `201` |
| `POST` | `/customers/{id}/accounts` | admin | Link an account: `{ "institution": "FNB", "externalAccountId": "62001001001" }` → `201`, or `200` if it was already linked |
| `DELETE` | `/customers/{id}/accounts?institution=FNB&externalAccountId=62001001001` | admin | Unlink → `204`, `404` if it wasn't linked |

`institution` must be a registered bank (`400` otherwise) and is stored in the bank's registered
spelling, so `fnb` links to `FNB`. A customer has at most 50 linked accounts. Every create, link
and unlink is audited with the acting admin, by customer id and reference; the name stays out of
the audit trail and the logs. In Development the API seeds a customer per demo household, and
three customers holding the mock aggregator's live-feed accounts at several banks (`CUST-0101` to
`CUST-0103`).

#### Webhooks

Transaction data is *received*, not polled — the account aggregator pushes new transactions to
us as they happen, rather than the API asking it for updates. There is no user-facing "sync"
button/endpoint.

| Method | Path | Auth | Description |
|---|---|---|---|
| `POST` | `/webhooks/bank-aggregator/transactions` | API key (`X-Api-Key` header) | Aggregator pushes new transactions for one account |

Auth is an API key, not a user's JWT — this is a server-to-server call with no logged-in
user involved. Sources and their keys are **database rows, not config** — a `WebhookSource` per
source, managed live through the admin UI (`/admin/webhook-sources`, staff with the Keycloak
`admin` realm role only) or its backing API (`/api/v1/admin/webhook-sources`). Only a SHA-256
hash of each key is ever stored; the plaintext is shown exactly once, when it's created or
rotated, and can't be retrieved afterwards. Keys are per-source, not one shared secret: if one
source's key leaks, only that entry needs rotating (from the UI, no redeploy), and whichever key
a request presents tells the API which source it actually came from (logged as `SourceName` on
every ingest — see `WebhookApiKeyEndpointFilter`/`ReceiveBankTransactionsCommand`), not just "someone
with a valid key." **Each source is one bank**, so the key also decides which
bank the transactions belong to. The payload names the account as the bank identifies it
(`externalAccountId`), and may name the bank (`institution`), which must then be the key's bank:
anything else is a 400 at the webhook, or dead-lettered and audited if it gets as far as processing. The body is versioned
(`schemaVersion`, current and default 2, and 1 is still accepted):

```http
POST /api/v1/webhooks/bank-aggregator/transactions
X-Api-Key: <a key from /admin/webhook-sources>
Content-Type: application/json

{
  "schemaVersion": 2,
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

#### Posted, pending and the ledger

Only posted transactions are recorded.
`status` is optional on each transaction:

| `status` | What happens |
|---|---|
| `posted`, or omitted | Recorded as a ledger entry, once. A replay is skipped as a duplicate, audited and alerted |
| `pending` | Acknowledged, audited as `transaction.pending_skipped`, **not recorded**. A pending authorisation can still change (a tip is added) or vanish, and a ledger entry never changes. The posting arrives later as its own `posted` item and is recorded with the bank's posted amount and date |

So there is one rule for every money figure: income, expenses, net and every aggregate are sums
of ledger entries in the requested currency. There is no pending state to settle, no expiry job
and no concurrency token, because nothing in the ledger is ever updated. Rows written by the
earlier pending lifecycle stay in the table untouched and are excluded from every read and index.

#### Normalization

Every delivery is normalized with its bank's profile before anything else looks at it. The
source that delivered it (the key) selects the profile. Rules live in `normalization-rules.json` (overridable per environment
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

- **Source and signature:** every record carries a **`source`** header naming an active
  webhook source (created at `/api/v1/admin/webhook-sources`) and a **`signature`** header:
  an ECDSA P-256 signature over the source, the idempotency key and the payload hash, made
  with the source's private key (`KafkaRecordSignature` defines the signed bytes). The platform verifies it
  against the public key registered with `PUT /api/v1/admin/webhook-sources/{id}/signing-key`.
  A record with no source, no signature, or one that doesn't verify is dead-lettered and
  audited as `unauthenticated`; there is no default source. Deactivating a source stops it on
  both channels.

- **Delivery:** at-least-once. An offset is committed only after the record is stored in the
  inbox or moved to the dead-letter topic.
- **Transient failures** (e.g. Postgres down) retry the same record with exponential backoff (capped
  by `Kafka:MaxRetryBackoffSeconds`), which pauses that partition until it succeeds.
- **Records that can never succeed** (malformed JSON, failed validation, unknown source) go to
  **`bank-transactions.dlq`** with `dlq-reason` / `dlq-original-*` headers.
- Topics are created on startup (`Kafka:CreateTopics`). The worker requires
  `ConnectionStrings:kafka`, because it also publishes integration events; `Kafka:Enabled=false`
  switches off only the inbound consumer.

#### Integration events (outbound)

Every ledger entry produces a **`TransactionRecorded` v1** event on the **`transaction-events`**
topic. It is written to the outbox in the same transaction as the entry and published by the
worker. It is keyed `{institution}:{externalAccountId}`, so one account's events stay in order,
and it carries the `message-id`, `event-type`, `schema-version`, `occurred-at`, `traceparent`
and `correlation-id` headers. Delivery is at least once; consumers deduplicate on `message-id`.

Browse and produce test records in Kafka UI (http://localhost:8083 in both docker-compose and
Aspire). From the host, the docker-compose broker is `localhost:9092`.

#### Idempotency and duplicate notifications

Duplicates are checked at two levels. Neither level fails the rest of a delivery:

| Level | Detected by | What happens |
|---|---|---|
| **Delivery** (same webhook call / Kafka record sent again) | `Idempotency-Key` HTTP header or `idempotency-key` Kafka header. Without one, the SHA-256 of the payload is used. Unique per source. | No second inbox row. The caller gets the original `inboxMessageId` with `isDuplicate: true`. If that original was dead-lettered, it's requeued with a fresh retry budget (`requeued: true`). The same key with a **different** payload is refused with `422` (`Inbox.IdempotencyKeyReused`; dead-lettered on Kafka), never acknowledged as a duplicate, because that would drop the new data. |
| **Transaction** (bank id already in the ledger for that account at that bank, or repeated inside one batch) | Unique partial index `IX_Transactions_Ledger_Key` on `(SourceName, ExternalAccountId, SourceExternalId)` (bank, account, bank id), plus a lookup before insert. The same id from another bank or account is a different transaction. | Only the duplicates are skipped; every other transaction in the batch is recorded. A concurrent insert that wins the race triggers a re-check and retry, not a dropped batch. |

Every duplicate at either level:

- increments `inbound_duplicates_total{source_name, level}`
- writes a `DuplicateInboundDetected` outbox message. The outbox dispatcher logs it (account id
  masked) and, if `NotificationOptions:DuplicateAlertWebhookUrl` is set, POSTs an alert there.
  It runs asynchronously, so a failing alert channel can't block ingestion. A failed alert is
  retried by the outbox (408, 429, 5xx) or dead-lettered (other 4xx), never silently dropped.
  The hook URL can embed a token, so it is never logged.

#### Audit trail (admin)

Every inbound delivery and every administrative change leaves an append-only trail in
`audit."AuditEvents"` (the Audit module).
It records where the data came from (channel and source), when, how (the channel's own
metadata), what happened to it afterwards, and for admin changes, who made them (`actor`, the
admin's Keycloak subject id).

| Method | Path | Description |
|---|---|---|
| `GET` | `/admin/audit/events` | Search, newest first. Filters: `channel`, `sourceName`, `eventType`, `actor`, `externalAccountId`, `inboxMessageId`, `transactionId`, `externalTransactionId`, `from`, `to`, `cursor`, `pageSize` (max 200), `includeTotal` |
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
| `inbound.processed` / `inbound.processing_failed` / `inbound.dead_lettered` | Inbox dispatcher outcome | `recordedCount`, `duplicateCount`, `pendingCount` / `attempt`, `maxAttempts`, `failureKind`, `nextAttemptAt` |
| `transaction.ingested` | A ledger entry was recorded | `institution`, `amount`, `currency`, `category` |
| `transaction.duplicate_skipped` | A posting already in the ledger (or repeated in the batch) | the bank id; reason in `detail` |
| `transaction.pending_skipped` | A pending notice, acknowledged and not recorded | the bank id; reason in `detail` |
| `admin.source_created` / `_updated` / `_key_rotated` / `_signing_key_registered` / `_activated` / `_deactivated` | An admin changed a bank source (channel `admin`, with `actor`) | the change, never key material |

Events that describe a database change are written in the same database transaction as
that change, so they appear the moment it commits and are never recorded for a change that
rolled back. All events of one delivery share its
`inboxMessageId`; `traceId` links to the request's logs and traces in Seq.

#### Admin — webhook sources

Manages the banks: the `WebhookSource` rows above, one per bank (who's allowed to call the
webhook, with what key, and how the bank is shown). The UI's **Banks** page does all of this.
Requires the Keycloak `admin` realm role — a `staff` token gets `403`.

| Method | Path | Description |
|---|---|---|
| `GET` | `/admin/webhook-sources` | List banks (never includes key material) |
| `POST` | `/admin/webhook-sources` | Add a bank: `{ "code": "Nedbank", "displayName": "Nedbank", "color": "#007A4D" }`. The code (letters, digits, `-`, `_`) is fixed and stamped on its transactions. The response includes the API key once |
| `PUT` | `/admin/webhook-sources/{id}` | Change a bank's display name and colour: `{ "displayName", "color" }` |
| `PUT` | `/admin/webhook-sources/{id}/signing-key` | Register the bank's Kafka signing key (ECDSA P-256) |
| `POST` | `/admin/webhook-sources/{id}/rotate` | Replace the key — old one stops working immediately |
| `POST` | `/admin/webhook-sources/{id}/activate` | Re-enable a deactivated source |
| `POST` | `/admin/webhook-sources/{id}/deactivate` | Disable without deleting (key stops authenticating) |

#### Health & Metrics

| Method | Path | Auth | Description |
|---|---|---|---|
| `GET` | `/liveness` | No | Process is responsive (no dependency I/O) — liveness/startup probe |
| `GET` | `/readiness` | No | Postgres reachable; Redis only degrades it (still 200); Seq never affects it |
| `GET` | `/health` | No | Every check, detailed in Development |
| `GET` | `/metrics` | No | Prometheus scrape endpoint — **port 9464 only** (`Metrics__Port`), never exposed publicly |

---

### Filter query parameters

`GET /transactions` accepts:

| Parameter | Type | Description |
|---|---|---|
| `cursor` | string | `nextCursor` from the previous page; omit for the first page |
| `pageSize` | int | Default `20`, max `100` |
| `includeTotal` | bool | Default `false`; adds `totalCount`, counted up to 10,000 (`totalCountCapped: true` beyond that) |
| `fromDate` | datetime | Transaction date lower bound |
| `toDate` | datetime | Transaction date upper bound |
| `category` | int | `0`=Uncategorized `1`=Groceries `2`=Dining `3`=Transportation `4`=Entertainment `5`=Utilities `6`=Housing `7`=Healthcare `8`=Income `9`=Transfer `10`=Shopping `11`=Subscriptions |
| `currency` | string | ISO 4217 code, upper case (e.g. `ZAR`, `USD`) |
| `minAmount` | decimal | Absolute amount lower bound |
| `maxAmount` | decimal | Absolute amount upper bound |
| `searchTerm` | string | 3–100 characters; case-insensitive substring of the description (trigram index) |
| `institution` | string | Bank code (the source that delivered it), e.g. `FNB`, `Absa`, `Capitec`, `StandardBank` |
| `externalAccountId` | string | The account, as the bank identifies it (requires `institution`) |
| `sortBy` | string | `date` (default) or `amount`, the two sorts backed by an index |
| `sortDescending` | bool | Default `true` |

---

### Rate limits

| Scope | Limit | Notes |
|---|---|---|
| Per endpoint group | 60 req / min per client | `/transactions`, `/admin/...` |
| Global (burst ceiling) | 100 req / min per client | All routes combined |

Returns `HTTP 429` with a `Retry-After` header when exceeded.
Client identity: authenticated username → remote IP → `anonymous`.
Counters live in Redis and are shared across all replicas.

---

## Configuration reference

Configuration is layered, later sources winning: `appsettings.json` →
`appsettings.{Environment}.json` → **`secrets.json`** → environment variables → command line.
Non-secret settings are tracked in `appsettings*.json`. Secrets live in a `secrets.json` next to
each host: git-ignored locally (copy `secrets.template.json`), and rendered by the Vault Agent in
clusters at the path in `Secrets:FilePath`, with the same keys
. Code reads both the same way, by key.
A configured `Secrets:FilePath` that does not exist stops the host at startup.

Environment variables use `__` as a section separator:
`ConnectionStrings__transactiondb` → `ConnectionStrings:transactiondb`.

| Key | Description | Where it comes from (compose) |
|---|---|---|
| `ConnectionStrings:transactiondb` | PostgreSQL connection string (secret) | built from `POSTGRES_PASSWORD` in `.env` |
| `ConnectionStrings:kafka` | Kafka bootstrap servers (worker: required) | `kafka:9093` |
| `Secrets:FilePath` | Absolute path of the secrets file (clusters: the Vault-rendered file) | not set: `secrets.json` in the content root, optional |
| `Kafka:IntegrationEventsTopic` | Topic for `TransactionRecorded` events | `transaction-events` |
| `NotificationOptions:DuplicateAlertWebhookUrl` | Optional alert hook (secret: it embeds a token) | `DUPLICATE_ALERT_WEBHOOK_URL` in `.env` |
| `ConnectionStrings:redis` | Redis connection string | `redis:6379` |
| `ConnectionStrings:seq` | Seq ingestion URL | `http://seq:80` |
| `Keycloak:Authority` | Keycloak server root the API itself calls (in-cluster/compose service address) | `http://keycloak:8080` |
| `Keycloak:PublicIssuer` | Full realm URL the browser uses — must match the `iss` claim on tokens | `http://localhost:8081/realms/transaction-aggregation` |
| `Keycloak:Realm` | Keycloak realm name | `transaction-aggregation` |
| `Keycloak:Audience` | Expected token audience | `transaction-ui` |
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

The readiness probe calls `/readiness` (Postgres; Redis only degrades it). If Postgres is
unreachable the pod waits out of rotation until it recovers.

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
