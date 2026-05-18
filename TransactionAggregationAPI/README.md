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
5. [Option A — Docker Compose](#option-a--docker-compose-quickest)
6. [Option B — .NET Aspire (debug)](#option-b--net-aspire-debug)
7. [Option C — Kubernetes](#option-c--kubernetes-rancher-desktop)
8. [API reference](#api-reference)
9. [Configuration reference](#configuration-reference)
10. [Troubleshooting](#troubleshooting)

---

## What this project does

- Aggregates transactions from multiple mock bank sources (BankA, BankB)
- Categorises transactions automatically by keyword (Groceries, Dining, Transport …)
- Exposes a versioned REST API (`/api/v1/…`) secured with JWT bearer tokens
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
| Auth | JWT Bearer tokens |
| API docs | Scalar (OpenAPI — Development only) |
| Orchestration | Docker Compose · .NET Aspire · Kubernetes (k3s / Rancher Desktop) |

---

## Project structure

```
TransactionAggregationAPI/              ← solution root
│
├── TransactionAggregationAPI/          ← Web API (entry point, also hosts Blazor in dev)
│   ├── Endpoints/                      ← Minimal API route handlers
│   ├── Middleware/                     ← Exception handling, request context logging
│   ├── RateLimiting/                   ← Redis-backed custom RateLimiter
│   ├── SeedData.cs                     ← 10 customers, 21 accounts, ~2 700 transactions
│   ├── Program.cs                      ← App bootstrap
│   └── appsettings.json
│
├── TransactionAggregationUI/           ← Blazor WASM frontend
│   ├── Pages/                          ← Dashboard, Accounts, Transactions, Login …
│   ├── Services/                       ← HTTP clients for each API resource
│   ├── Auth/                           ← JWT auth state provider
│   ├── wwwroot/appsettings.json        ← ApiBaseUrl (empty = derive from host)
│   ├── nginx.conf                      ← Proxies /api/ to the API service
│   └── Dockerfile                      ← nginx + published WASM static files
│
├── TransactionAggregationAPI.AppHost/  ← .NET Aspire orchestration
│   ├── AppHost.cs                      ← Wires up API + Postgres + Redis + Seq
│   └── appsettings.Development.json    ← Fixed postgres-password parameter
│
├── TransactionAggregation.Application/ ← Use cases (CQRS / MediatR)
├── TransactionAggregation.Domain/      ← Entities, value objects, enums
├── TransactionAggregation.Infrastructure/ ← Redis cache, bank adapters
├── TransactionAggregation.Persistence/    ← EF Core DbContext, migrations
├── TransactionAggregationAPI.ServiceDefaults/ ← Health checks, OpenTelemetry, prometheus-net
│
├── monitoring/                         ← Docker Compose monitoring stack
│   ├── prometheus.yml                  ← Prometheus scrape config (targets api:8080/metrics)
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
    ├── postgres/ redis/ seq/           ← StatefulSets + Services (persistent volumes)
    ├── api/                            ← Deployment, Service, Ingress, HPA, PDB, migration Job
    ├── ui/                             ← Deployment, Service, Ingress, ConfigMap
    ├── dev-tools/                      ← pgAdmin, Redis Commander (optional --dev-tools)
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
| **UI + API** | http://localhost:5001 |
| **API docs** (Scalar) | http://localhost:5001/scalar/v1 |
| **Seq** structured logs | Linked from Aspire dashboard |

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
| **UI** (nginx + Blazor WASM) *(`--ui`)* | 2 | Serves frontend, proxies `/api/` to the API service |
| **Prometheus** *(`--monitoring`)* | 1 | Pod annotation-based scrape discovery |
| **Grafana** *(`--monitoring`)* | 1 | Pre-provisioned dashboard + Prometheus datasource |
| **pgAdmin** *(`--dev-tools`)* | 1 | PostgreSQL browser |
| **Redis Commander** *(`--dev-tools`)* | 1 | Redis key browser |

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

# API image
nerdctl --namespace k8s.io build \
  -t transactionaggregationapi:latest \
  -f TransactionAggregationAPI/Dockerfile \
  .

# UI image (only needed if you plan to pass --ui)
nerdctl --namespace k8s.io build \
  -t transactionaggregationui:latest \
  -f TransactionAggregationUI/Dockerfile \
  .

# Confirm images are present
nerdctl --namespace k8s.io images | grep transaction
```

### Step 2 — Fill in secrets

Open `k8s/secrets.yaml` and replace the three `CHANGE_ME_BASE64_ENCODED` values:

```bash
# PostgreSQL password
echo -n 'YourStrongPassword123!' | base64

# JWT signing key (random, at least 32 chars)
openssl rand -hex 32 | base64

# pgAdmin password
echo -n 'YourAdminPassword!' | base64
```

The other three values (`jwt-issuer`, `jwt-audience`, `jwt-expiration-minutes`)
are already correct — leave them as-is.

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

> Seq has no Kubernetes Ingress — use port-forward to access logs:
> `kubectl port-forward svc/seq 5341:80 -n transaction-aggregation`

> Scalar API docs (`/scalar/v1`) are not available in Kubernetes — the ConfigMap
> sets `ASPNETCORE_ENVIRONMENT=Production`. Use Option A or B for API exploration.

**On Windows (Rancher Desktop): add the hostnames to `C:\Windows\System32\drivers\etc\hosts` as `127.0.0.1` entries** — see the troubleshooting section below for the exact lines.

**No hostnames working? Use port-forward instead:**

```bash
kubectl port-forward svc/transaction-api  8080:80    -n transaction-aggregation
kubectl port-forward svc/transaction-ui   7200:80    -n transaction-aggregation  # if --ui
kubectl port-forward svc/seq              5341:80    -n transaction-aggregation  # logs (no Ingress)
kubectl port-forward svc/prometheus       9090:9090  -n transaction-aggregation  # if --monitoring
kubectl port-forward svc/grafana          3000:80    -n transaction-aggregation  # if --monitoring
```

### Rebuilding after a code change

```bash
# API or backend code changed
nerdctl --namespace k8s.io build \
  -t transactionaggregationapi:latest \
  -f TransactionAggregationAPI/Dockerfile .
kubectl rollout restart deployment/transaction-api -n transaction-aggregation
kubectl rollout status  deployment/transaction-api -n transaction-aggregation

# UI (Blazor pages, nginx.conf) changed
nerdctl --namespace k8s.io build \
  -t transactionaggregationui:latest \
  -f TransactionAggregationUI/Dockerfile .
kubectl rollout restart deployment/transaction-ui -n transaction-aggregation
kubectl rollout status  deployment/transaction-ui -n transaction-aggregation

# New EF Core migration added — migrations apply automatically on pod restart
kubectl rollout restart deployment/transaction-api -n transaction-aggregation
kubectl rollout status  deployment/transaction-api -n transaction-aggregation
```

### Tear down

```bash
./deploy-k8s.sh --teardown
# or manually:
kubectl delete namespace transaction-aggregation
```

---

## API reference

### Authentication

All endpoints except registration and login require a JWT bearer token:

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

**Log in:**

```http
POST /api/v1/customers/login
Content-Type: application/json

{
  "username": "jane@example.com",
  "password": "SecurePassword123!"
}
```

The response body contains `{ "token": "eyJ..." }`. Include that value in
the `Authorization` header for all subsequent requests.

---

### Endpoints

All routes are prefixed with `/api/v1`.

#### Customers

| Method | Path | Auth | Description |
|---|---|---|---|
| `POST` | `/customers` | No | Register |
| `POST` | `/customers/login` | No | Log in, receive JWT |
| `GET` | `/customers` | Yes | List all (paginated) |
| `GET` | `/customers/{id}` | Yes | Get by ID |
| `GET` | `/customers/email/{email}` | Yes | Get by email |
| `PUT` | `/customers/{id}` | Yes | Update name / email |
| `DELETE` | `/customers/{id}` | Yes | Delete account |

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
| `POST` | `/customers/{id}/transactions` | Yes | Create transaction |
| `POST` | `/customers/{id}/transactions/sync` | Yes | Sync from all bank sources |
| `GET` | `/customers/{id}/transactions/filter` | Yes | Paginated + filtered list |
| `GET` | `/customers/{id}/transactions/summary` | Yes | Income / expenses / monthly breakdown |
| `GET` | `/customers/{id}/transactions/export` | Yes | Download as CSV |

#### Transactions (standalone)

| Method | Path | Auth | Description |
|---|---|---|---|
| `GET` | `/transactions/{id}` | Yes | Get by ID |
| `PATCH` | `/transactions/{id}/categorize` | Yes | Override category |

#### Health & Metrics

| Method | Path | Auth | Description |
|---|---|---|---|
| `GET` | `/health` | No | Readiness — checks Postgres + Redis |
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
| `source` | string | `BankA` / `BankB` / `Internal` |
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
| `Jwt:Secret` | Signing key — min 32 chars | Set in `appsettings.Development.json` |
| `Jwt:Issuer` | JWT issuer claim | `TransactionAggregationAPI` |
| `Jwt:Audience` | JWT audience claim | `TransactionAggregationAPIClients` |
| `Jwt:ExpirationInMinutes` | Token lifetime | `60` |
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

EF Core migrations run automatically on startup. If migrations fail the pod
will not become Ready — check the logs for migration errors:

```bash
kubectl logs -f deployment/transaction-api -n transaction-aggregation | grep -i migrat
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

Tokens expire after 60 minutes. Log in again to get a fresh token.
In Kubernetes, verify `k8s/secrets.yaml` has correct base64 values for
`jwt-secret`, `jwt-issuer`, and `jwt-audience`.

### Rate limit 429 on every request

Limits: 60 req/min per endpoint group, 100 req/min global.
To raise temporarily for testing:

- Global → `Program.cs` `AddOptions<RateLimiterOptions>` block: `PermitLimit = 3000`
- Endpoint policy → `RateLimiting/RedisFixedWindowPolicy.cs`: `PermitLimit = 600`

Rebuild and redeploy. Revert before committing.
