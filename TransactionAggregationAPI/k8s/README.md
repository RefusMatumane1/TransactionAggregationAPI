# Kubernetes manifests

Plain manifests, applied in order by `deploy-k8s.sh` at the repository root. Every environment
uses the same manifests. The image tag is the only thing that varies, filled in by the script
from `IMAGE_TAG` (default: the current git short sha). Full usage is in the main README
(Option C).

## Layout

```
k8s/
├── namespace.yaml
├── secrets.yaml                 # Placeholders; deploy-k8s.sh refuses to deploy until they are replaced
├── configmap.yaml               # Non-secret config shared by the API and the worker
├── network-policy.yaml          # Pod-level traffic rules (PostgreSQL reachable only from PgBouncer and the Job)
├── postgres/                    # PostgreSQL 17.6 StatefulSet + PVC; init-roles.yaml creates tagg_migrator / tagg_app
├── pgbouncer/                   # Transaction pooling, 2 replicas, PDB
├── redis/  seq/  kafka/         # StatefulSets + Services (Kafka: KRaft single node)
├── keycloak/                    # Realm import (admin user, institutions attribute and mapper), Deployment, PVC, Ingress
├── api/
│   ├── migration-job.yaml       # db-migrate Job: runs before every rollout, as the schema owner
│   ├── deployment.yaml          # 2+ replicas, probes, maxUnavailable 0, non-root, read-only FS
│   └── service.yaml  ingress.yaml  hpa.yaml  pdb.yaml
├── worker/
│   ├── configmap.yaml           # Worker-only config: Kafka topics, inbox/outbox, archive, alerts
│   ├── deployment.yaml          # Background processing; serves only probes and /metrics
│   ├── scaledobject.yaml        # KEDA on inbox + outbox backlog (2–30)
│   └── hpa.yaml  pdb.yaml       # CPU fallback without KEDA
├── vault/                       # Vault Agent patches + policy: secrets.json rendered from Vault (--vault)
├── ui/                          # nginx + Blazor WASM (--ui)
├── monitoring/                  # Prometheus (rules included) + Grafana (--monitoring)
└── dev-tools/                   # pgAdmin, Redis Commander, Kafka UI (--dev-tools)
```

## Deploy

```bash
./deploy-k8s.sh                              # core: data stores, migrations, PgBouncer, API, worker
./deploy-k8s.sh --ui --monitoring --dev-tools
./deploy-k8s.sh --vault                      # API and worker secrets from Vault (vault/README.md)
./deploy-k8s.sh --teardown                   # delete the namespace and all data
```

The order matters and the script enforces it: data stores → `db-migrate` Job (the deployment
stops if it fails) → PgBouncer → API (readiness-gated) → worker → network policies → smoke test.
To apply by hand, follow the same order and wait for the Job to complete before the API.

## Secrets

| Mode | Where secrets come from |
|---|---|
| `--vault` | The Vault Agent Injector renders each pod's `secrets.json` (same shape as the local file). The patches remove the Secret-based connection-string env entries, so Vault is the only source ([vault/README.md](vault/README.md)) |
| default | `transaction-api-secrets` from `secrets.yaml`: replace every `CHANGE_ME_BEFORE_DEPLOY` value; never commit real values (`git update-index --assume-unchanged k8s/secrets.yaml`) |

The migration Job and Keycloak always read the k8s Secret: the migrator password and the
Keycloak bootstrap credentials are cluster-operator secrets, not application configuration.

## Assumptions

| Area | Assumption |
|---|---|
| Ingress controller | `traefik` (the k3s/Rancher Desktop default) |
| StorageClass | `local-path` (ships with k3s) |
| TLS | cert-manager with a `selfsigned-issuer` ClusterIssuer locally; HTTPS ends at the ingress |
| Images | built into the `k8s.io` containerd namespace (`imagePullPolicy: IfNotPresent`); push to a registry for shared clusters |
| metrics-server | installed (required for HPAs) |
| NetworkPolicy | enforced only by a CNI that supports it (Calico, Cilium, Canal); k3s's default Flannel does not |
| KEDA | optional; without it the worker uses the CPU HPA |

## Known limits

- **PostgreSQL and Redis are single replicas.** A node failure means downtime until the node
  recovers. For production use a managed service or an operator (CloudNativePG; Redis Sentinel).
- **Seq has no authentication** in this setup and no Ingress (port-forward only). Use an
  authenticated log backend in production.
- **No Alertmanager.** Prometheus loads the alert rules (backlog, dead letters, consumer crashes,
  5xx, latency, targets down), but a firing alert is visible only on its `/alerts` page.
- **Upgrading from PostgreSQL 16:** the StatefulSet runs 17.6, which cannot open a 16 data
  directory. Dump with `pg_dump -Fc` and restore with `pg_restore` on a fresh volume.

## Observability

| Signal | How |
|---|---|
| Logs | Serilog → Seq (`http://seq:80`), with correlation id, trace id, user id and (worker) inbox message id on every event |
| Metrics | prometheus-net on port 9464 (not in the Service or Ingress); Prometheus discovers pods by annotation |
| Traces | OpenTelemetry (ASP.NET Core, EF Core, Redis, HttpClient with redacted URLs, Kafka trace context through the inbox and outbox). Set `OTEL_EXPORTER_OTLP_ENDPOINT` to export |
| Health | `/liveness` (process), `/readiness` (PostgreSQL; Redis only degrades), `/health` (all checks) |
