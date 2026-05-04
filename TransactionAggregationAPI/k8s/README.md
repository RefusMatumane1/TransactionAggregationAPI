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
│   └── service.yaml             # ClusterIP Service (ports 80 + 5341)
│
├── api/
│   ├── deployment.yaml          # 2-replica Deployment, probes, rolling update
│   ├── service.yaml             # ClusterIP Service
│   ├── ingress.yaml             # Traefik Ingress for API + Seq UI
│   ├── hpa.yaml                 # HPA (CPU 70%, Memory 80%, min 2 / max 10)
│   ├── pdb.yaml                 # PodDisruptionBudget (minAvailable: 1)
│   └── migration-job.yaml       # EF Core migration Job (Helm pre-install hook)
│
├── dev-tools/
│   ├── pgadmin/
│   │   ├── deployment.yaml
│   │   └── service.yaml         # ClusterIP + Ingress (IP-whitelisted)
│   └── redis-commander/
│       ├── deployment.yaml
│       └── service.yaml         # ClusterIP + Ingress (IP-whitelisted)
│
└── helm/
    ├── Chart.yaml
    ├── values.yaml              # Production-safe defaults
    ├── values.dev.yaml          # Local/dev overrides
    ├── values.staging.yaml      # Staging overrides
    └── values.production.yaml   # Production overrides
```

---

## Quick-start (local Rancher cluster)

```bash
# 1. Create the namespace
kubectl apply -f k8s/namespace.yaml

# 2. Populate and apply secrets (never commit real values)
#    Generate base64 values:
#      echo -n 'MyStr0ngP@ss!' | base64
#    Then edit k8s/secrets.yaml and apply:
kubectl apply -f k8s/secrets.yaml

# 3. Apply ConfigMap
kubectl apply -f k8s/configmap.yaml

# 4. Deploy data stores
kubectl apply -f k8s/postgres/
kubectl apply -f k8s/redis/
kubectl apply -f k8s/seq/

# 5. Wait for PostgreSQL to be ready
kubectl rollout status statefulset/postgres -n transaction-aggregation

# 6. Run database migration
kubectl apply -f k8s/api/migration-job.yaml
kubectl wait --for=condition=complete job/db-migrate \
  -n transaction-aggregation --timeout=120s

# 7. Deploy the API
kubectl apply -f k8s/api/

# 8. (Dev only) Deploy dev tools
kubectl apply -f k8s/dev-tools/

# 9. Apply network policies
kubectl apply -f k8s/network-policy.yaml
```

### Using Helm (recommended)

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

### 2. ~~EF Core migrations race condition~~ — FIXED

**File:** `TransactionAggregationAPI/Program.cs`

Two changes were made:

**`--migrate-only` flag** — when this argument is present the app applies all pending
migrations via `MigrateAsync()` and exits with code 0 without starting the web server.
The Kubernetes Job (`migration-job.yaml`) passes this flag and is run as a Helm
`pre-install,pre-upgrade` hook, so migrations complete before any API replica starts.

**Startup guard** — the unconditional `ApplyMigrationsAsync()` call is now gated:
```csharp
// --migrate-only: migrate and exit (Kubernetes Job path)
if (args.Contains("--migrate-only"))
{
    await app.ApplyMigrationsAsync();
    return;
}

// In Production/Staging, migrations are owned by the pre-deploy Job.
if (!app.Environment.IsProduction() && !app.Environment.IsStaging())
    await app.ApplyMigrationsAsync();
```

This preserves the convenient auto-migration behaviour for local Development and
Docker Compose (single process, no race) while eliminating it for multi-replica
cluster environments.

---

### 3. ~~In-memory rate limiting is per-pod~~ — FIXED

**Files:** `TransactionAggregationAPI/RateLimiting/` (3 new files), `Program.cs`

Both limiters are now backed by Redis using a custom `RateLimiter` implementation.
No new NuGet packages were added — `IConnectionMultiplexer` was already registered.

**`RedisFixedWindowRateLimiter`** — core limiter using an atomic Lua script
(`INCR` + `PEXPIRE`). All counter state lives in Redis, so the limit is enforced
consistently across every replica. Falls back to allow-all if Redis is unreachable.

**`RedisFixedWindowPolicy`** (`IRateLimiterPolicy<string>`) — registered as a
singleton, injected with `IConnectionMultiplexer` by the DI container.
Handles the named `"FixedWindow"` policy (10 req/min) used by all endpoint groups.

**Global limiter** (100 req/min) — wired via `AddOptions<RateLimiterOptions>().Configure<IConnectionMultiplexer>(...)`.
This is the idiomatic pattern for accessing DI services inside options configuration
without calling `BuildServiceProvider()` a second time.

Redis key scheme:
- `ratelimit:global:{identity}` — global limiter (300/min)
- `ratelimit:endpoint:{identity}` — endpoint named policy (60/min)

where `{identity}` is the authenticated username → remote IP → `"anonymous"`.

---

### 4. PostgreSQL and Redis are single-replica (not HA)
**Problem:** The StatefulSets are `replicas: 1`. A node failure means database downtime.

**Fix options:**
- **PostgreSQL HA:** Use the [CloudNativePG operator](https://cloudnative-pg.io/) for automatic failover, or Bitnami's PostgreSQL HA chart.
- **Redis HA:** Use Bitnami's Redis chart with Sentinel mode or Redis Cluster.
- **Managed services:** Use AWS RDS / Azure Database for PostgreSQL and AWS ElastiCache / Azure Cache for Redis in staging/production.

---

### 5. Seq (OSS) has no authentication in local mode
**Problem:** The free tier of Seq has no built-in user authentication. Anyone who can reach the Seq Ingress URL can read all logs (including JWT tokens and request bodies logged at Debug level).

**Fix:** The Seq Ingress is IP-whitelisted to RFC-1918 private ranges. For production, disable the Ingress (`seq.ingress.enabled: false` in `values.production.yaml`) and use `kubectl port-forward` for ad-hoc access, or replace Seq with **Grafana Loki** (cloud-native, horizontally scalable, RBAC-capable).

---

### 6. `docker-compose.override.yml` mounts host user-secrets
**Problem:**
```yaml
volumes:
  - ${HOME}/.microsoft/usersecrets:/home/app/.microsoft/usersecrets:ro
  - ${HOME}/.aspnet/https:/home/app/.aspnet/https:ro
```
Host path mounts do not exist in a Kubernetes cluster. The developer's local user secrets and HTTPS certificate are not available on cluster nodes.

**Fix (already applied):** Secrets are injected via Kubernetes Secrets as environment variables. HTTPS is terminated at the Ingress controller — the API pod speaks plain HTTP internally.

---

### 7. `platform: linux/amd64` on Seq
The Compose file forces Seq to `linux/amd64`. On Apple Silicon (ARM) this triggers QEMU emulation, which is slow. In Kubernetes on an x86 cluster this is irrelevant. If your Rancher nodes are ARM, pin `seq` to an ARM-compatible image tag or use an alternative log aggregator.

---

## Observability summary

| Signal | How it works |
|---|---|
| **Structured logs** | Serilog → Seq (internal `http://seq:80`). Add OTEL log exporter via `OTEL_EXPORTER_OTLP_ENDPOINT` for external collectors |
| **Metrics** | OpenTelemetry metrics + ASP.NET Core runtime metrics emitted on `:8080`. Annotated for Prometheus scraping (`prometheus.io/scrape: "true"`) |
| **Traces** | OpenTelemetry distributed tracing (ASP.NET Core + EF Core + Redis + HttpClient). Enable OTLP export by setting `OTEL_EXPORTER_OTLP_ENDPOINT` in ConfigMap |
| **Health** | `/alive` (liveness — self tag only, fast), `/health` (readiness — includes Postgres + Redis checks via `AspNetCore.HealthChecks.*`) |
