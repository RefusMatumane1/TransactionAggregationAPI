# Kubernetes Manifests — Transaction Aggregation API

## Directory structure

```
k8s/
├── namespace.yaml               # Namespace definition
├── secrets.yaml                 # Secret scaffolding (fill & seal before commit)
├── configmap.yaml               # Non-sensitive environment config
├── network-policy.yaml          # Pod-level traffic rules
│
├── postgres/
│   ├── statefulset.yaml         # Single-replica StatefulSet + PVC
│   └── service.yaml             # ClusterIP Service
│
├── redis/
│   ├── statefulset.yaml         # Single-replica StatefulSet + PVC
│   └── service.yaml             # ClusterIP Service
│
├── seq/
│   ├── statefulset.yaml         # Single-replica StatefulSet + PVC
│   └── service.yaml             # ClusterIP Service (no Ingress; access via port-forward)
│
├── api/
│   ├── deployment.yaml          # 2-replica Deployment, probes, rolling update
│   ├── service.yaml             # ClusterIP Service
│   ├── ingress.yaml             # Traefik Ingress → api.transaction.local
│   ├── hpa.yaml                 # HPA (CPU 70%, Memory 80%, min 2 / max 10)
│   ├── pdb.yaml                 # PodDisruptionBudget (minAvailable: 1)
│   └── migration-job.yaml       # EF Core migration Job (optional pre-deploy alternative)
│
├── ui/
│   ├── configmap.yaml           # nginx upstream config
│   ├── deployment.yaml          # 2-replica nginx + Blazor WASM
│   ├── service.yaml             # ClusterIP Service
│   └── ingress.yaml             # Traefik Ingress → ui.transaction.local
│
├── dev-tools/
│   ├── pgadmin/
│   │   ├── deployment.yaml
│   │   └── service.yaml         # ClusterIP + Ingress (IP-whitelisted)
│   └── redis-commander/
│       ├── deployment.yaml
│       └── service.yaml         # ClusterIP + Ingress (IP-whitelisted)
│
├── monitoring/
│   ├── prometheus/
│   │   ├── serviceaccount.yaml  # ServiceAccount for pod discovery
│   │   ├── clusterrole.yaml     # ClusterRole: get/list/watch nodes, pods, services
│   │   ├── clusterrolebinding.yaml
│   │   ├── configmap.yaml       # prometheus.yml with kubernetes_sd_configs
│   │   ├── deployment.yaml      # Single-replica Prometheus
│   │   ├── service.yaml         # ClusterIP on port 9090
│   │   └── ingress.yaml         # Traefik Ingress → prometheus.transaction.local
│   └── grafana/
│       ├── configmap-provisioning.yaml   # Datasource (Prometheus) + dashboard provider
│       ├── configmap-dashboards.yaml     # transaction-api.json dashboard (prometheus-net metrics)
│       ├── pvc.yaml             # 1 Gi PVC for Grafana state
│       ├── deployment.yaml      # Single-replica Grafana (runAsUser 472)
│       ├── service.yaml         # ClusterIP port 80 → 3000
│       └── ingress.yaml         # Traefik Ingress → grafana.transaction.local
│
└── helm/
    ├── Chart.yaml
    ├── values.yaml              # Production-safe defaults
    ├── values.dev.yaml          # Local/dev overrides
    ├── values.development.yaml  # Development overrides
    ├── values.staging.yaml      # Staging overrides
    └── values.production.yaml   # Production overrides
```

---

## Recommended deployment (deploy-k8s.sh)

The script at the repo root handles ordering, waits, and /etc/hosts automatically.
See the main README for full usage — this is the quickest path:

```bash
# Core stack (API + data stores)
./deploy-k8s.sh

# With optional components
./deploy-k8s.sh --ui --monitoring --dev-tools
```

---

## Manual kubectl deployment

If you prefer to apply manifests yourself:

```bash
# 1. Namespace
kubectl apply -f k8s/namespace.yaml

# 2. Secrets + ConfigMaps (populate secrets.yaml first!)
kubectl apply -f k8s/secrets.yaml
kubectl apply -f k8s/configmap.yaml

# 3. Data stores — wait for both before applying the API
kubectl apply -f k8s/postgres/
kubectl apply -f k8s/redis/
kubectl apply -f k8s/seq/
kubectl rollout status statefulset/postgres -n transaction-aggregation
kubectl rollout status statefulset/redis    -n transaction-aggregation

# 4. API (migrations run automatically on startup)
kubectl apply -f k8s/api/service.yaml
kubectl apply -f k8s/api/deployment.yaml
kubectl apply -f k8s/api/ingress.yaml
kubectl apply -f k8s/api/hpa.yaml
kubectl apply -f k8s/api/pdb.yaml
kubectl rollout status deployment/transaction-api -n transaction-aggregation

# 5. UI (optional)
kubectl apply -f k8s/ui/configmap.yaml
kubectl apply -f k8s/ui/deployment.yaml
kubectl apply -f k8s/ui/service.yaml
kubectl apply -f k8s/ui/ingress.yaml

# 6. Network policies
kubectl apply -f k8s/network-policy.yaml

# 7. Monitoring (optional) — RBAC must be applied before the Deployment
kubectl apply -f k8s/monitoring/prometheus/serviceaccount.yaml
kubectl apply -f k8s/monitoring/prometheus/clusterrole.yaml
kubectl apply -f k8s/monitoring/prometheus/clusterrolebinding.yaml
kubectl apply -f k8s/monitoring/prometheus/configmap.yaml
kubectl apply -f k8s/monitoring/prometheus/deployment.yaml
kubectl apply -f k8s/monitoring/prometheus/service.yaml
kubectl apply -f k8s/monitoring/prometheus/ingress.yaml
kubectl apply -f k8s/monitoring/grafana/configmap-provisioning.yaml
kubectl apply -f k8s/monitoring/grafana/configmap-dashboards.yaml
kubectl apply -f k8s/monitoring/grafana/pvc.yaml
kubectl apply -f k8s/monitoring/grafana/deployment.yaml
kubectl apply -f k8s/monitoring/grafana/service.yaml
kubectl apply -f k8s/monitoring/grafana/ingress.yaml

# 8. Dev tools (optional)
kubectl apply -f k8s/dev-tools/
```

### Using Helm (recommended for multi-environment)

```bash
# Dev
helm upgrade --install transaction-aggregation ./k8s/helm \
  -f k8s/helm/values.yaml \
  -f k8s/helm/values.dev.yaml \
  -n transaction-aggregation --create-namespace

# Production
helm upgrade --install transaction-aggregation ./k8s/helm \
  -f k8s/helm/values.yaml \
  -f k8s/helm/values.production.yaml \
  --set api.image.tag=1.2.3 \
  -n transaction-aggregation --create-namespace
```

---

## Docker Compose → Kubernetes concept mapping

| Docker Compose concept | Kubernetes equivalent |
|---|---|
| `networks:` bridge driver | Kubernetes pod networking (flat network); NetworkPolicy for isolation |
| `depends_on: condition: service_healthy` | Readiness probes + init containers; Kubernetes does not block pod start natively |
| `.env` file / environment block | ConfigMap (non-sensitive) + Secret (sensitive) |
| `volumes:` named volume | PersistentVolumeClaim (bound to a StorageClass) |
| `restart: unless-stopped` | `restartPolicy: Always` (default for Deployments) |
| `container_name:` | Pod name (auto-generated); use labels for selection |
| `healthcheck:` | `livenessProbe` / `readinessProbe` / `startupProbe` |
| `ports: "host:container"` | Service + Ingress (NodePort/LoadBalancer for direct exposure) |

---

## Assumptions

| Area | Assumption |
|---|---|
| **Ingress controller** | `traefik` (k3s/Rancher Desktop default). Manifests already use `ingressClassName: traefik`. To use nginx instead, install `ingress-nginx` and change the class in all ingress files. |
| **StorageClass** | `local-path` (Rancher Local Path Provisioner, ships with k3s/RKE2). Change to your cluster's default if different |
| **TLS / cert-manager** | cert-manager is installed with a `selfsigned-issuer` ClusterIssuer for local dev. Remove the `tls:` block and cert-manager annotation if not installed |
| **Container registry** | Image is built locally (`imagePullPolicy: IfNotPresent`). Push to a registry and update `image.repository` for shared clusters |
| **metrics-server** | Installed (required for HPA). Rancher Desktop includes it; verify with `kubectl top nodes` |
| **NetworkPolicy enforcement** | Requires a CNI that enforces NetworkPolicy (Calico, Cilium, Canal). k3s default Flannel does NOT enforce them |
| **Secrets management** | Secrets are applied manually. For production, use [Sealed Secrets](https://github.com/bitnami-labs/sealed-secrets) or [External Secrets Operator](https://external-secrets.io/) |

---

## What does NOT translate cleanly to Kubernetes

### 1. ~~Health endpoints gated to `Development` only~~ — FIXED

**File:** `TransactionAggregationAPI.ServiceDefaults/Extensions.cs`

The `IsDevelopment()` guard was removed. Both endpoints are now mapped unconditionally:

- **`/alive`** — always returns plain-text `Healthy`/`Unhealthy` (no internal details).
- **`/health`** — returns plain-text status in Production/Staging; returns full JSON
  (check names, durations, exception messages) only in Development.

---

### 2. ~~In-memory rate limiting is per-pod~~ — FIXED

**Files:** `TransactionAggregationAPI/RateLimiting/` (3 new files), `Program.cs`

Both limiters are now backed by Redis using a custom `RateLimiter` implementation.
No new NuGet packages were added — `IConnectionMultiplexer` was already registered.

**`RedisFixedWindowRateLimiter`** — core limiter using an atomic Lua script
(`INCR` + `PEXPIRE`). All counter state lives in Redis, so the limit is enforced
consistently across every replica. Falls back to allow-all if Redis is unreachable.

**`RedisFixedWindowPolicy`** (`IRateLimiterPolicy<string>`) — registered as a
singleton, injected with `IConnectionMultiplexer` by the DI container.
Handles the named `"FixedWindow"` policy used by all endpoint groups.

**Global limiter** (100 req/min) — wired via `AddOptions<RateLimiterOptions>().Configure<IConnectionMultiplexer>(...)`.

Redis key scheme:
- `ratelimit:global:{identity}` — global limiter
- `ratelimit:endpoint:{identity}` — endpoint named policy

where `{identity}` is the authenticated username → remote IP → `"anonymous"`.

---

### 3. PostgreSQL and Redis are single-replica (not HA)

**Problem:** The StatefulSets are `replicas: 1`. A node failure means database downtime.

**Fix options:**
- **PostgreSQL HA:** Use the [CloudNativePG operator](https://cloudnative-pg.io/) for automatic failover, or Bitnami's PostgreSQL HA chart.
- **Redis HA:** Use Bitnami's Redis chart with Sentinel mode or Redis Cluster.
- **Managed services:** Use AWS RDS / Azure Database for PostgreSQL and AWS ElastiCache / Azure Cache for Redis in staging/production.

---

### 4. Seq (OSS) has no authentication in local mode

**Problem:** The free tier of Seq has no built-in user authentication. Anyone who can reach the Seq service can read all logs.

**Note:** Seq has no Kubernetes Ingress in this setup — access is via `kubectl port-forward` only, which limits exposure. For production, replace Seq with **Grafana Loki** (horizontally scalable, RBAC-capable) or another log aggregator with authentication.

---

### 5. `docker-compose.override.yml` mounts host user-secrets

**Problem:**
```yaml
volumes:
  - ${HOME}/.microsoft/usersecrets:/home/app/.microsoft/usersecrets:ro
  - ${HOME}/.aspnet/https:/home/app/.aspnet/https:ro
```
Host path mounts do not exist in a Kubernetes cluster. The developer's local user secrets and HTTPS certificate are not available on cluster nodes.

**Fix (already applied):** Secrets are injected via Kubernetes Secrets as environment variables. HTTPS is terminated at the Ingress controller — the API pod speaks plain HTTP internally.

---

### 6. `platform: linux/amd64` on Seq

The Compose file forces Seq to `linux/amd64`. On Apple Silicon (ARM) this triggers QEMU emulation, which is slow. In Kubernetes on an x86 cluster this is irrelevant. If your Rancher nodes are ARM, pin `seq` to an ARM-compatible image tag or use an alternative log aggregator.

---

## Observability summary

| Signal | How it works |
|---|---|
| **Structured logs** | Serilog → Seq (`http://seq:80`). Set `OTEL_EXPORTER_OTLP_ENDPOINT` to additionally send logs to any OTLP-compatible collector |
| **Metrics** | prometheus-net.AspNetCore serves `/metrics` at `:8080`. Prometheus discovers pods via `prometheus.io/scrape: "true"` annotation and scrapes the endpoint. Grafana visualises the data using the pre-provisioned dashboard. |
| **Traces** | OpenTelemetry distributed tracing (ASP.NET Core + EF Core + Redis + HttpClient). Set `OTEL_EXPORTER_OTLP_ENDPOINT` in ConfigMap to export traces to Jaeger, Tempo, or any OTLP endpoint. |
| **Health** | `/alive` (liveness — self tag only, fast), `/health` (readiness — includes Postgres + Redis checks) |
