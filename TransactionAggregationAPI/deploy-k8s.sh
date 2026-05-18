#!/usr/bin/env bash
# =============================================================================
# deploy-k8s.sh
# Deploy the Transaction Aggregation stack to a local Kubernetes cluster
# (Rancher Desktop / k3s).
#
# Assumes the API image is already built into the k8s.io containerd namespace:
#   transactionaggregationapi:latest
# Pass --ui to also deploy the Blazor WASM frontend (requires ui image too).
#
# Usage:
#   ./deploy-k8s.sh                  Deploy core stack  (API + data stores)
#   ./deploy-k8s.sh --ui             Also deploy the Blazor WASM UI
#   ./deploy-k8s.sh --dev-tools      Also deploy pgAdmin + Redis Commander
#   ./deploy-k8s.sh --monitoring     Also deploy Prometheus + Grafana
#   ./deploy-k8s.sh --skip-hosts     Skip /etc/hosts update
#   ./deploy-k8s.sh --teardown       Delete the namespace (removes all data)
#   ./deploy-k8s.sh --help
# =============================================================================
set -euo pipefail

# ── Colours ───────────────────────────────────────────────────────────────────
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
BOLD='\033[1m'
NC='\033[0m'

# ── Logging helpers ───────────────────────────────────────────────────────────
log_step()  { echo -e "\n${BOLD}${CYAN}▶  $*${NC}"; }
log_ok()    { echo -e "   ${GREEN}✔${NC}  $*"; }
log_info()  { echo -e "   ${BLUE}·${NC}  $*"; }
log_warn()  { echo -e "   ${YELLOW}⚠${NC}  $*"; }
log_error() { echo -e "   ${RED}✘${NC}  $*" >&2; }

# ── Config ────────────────────────────────────────────────────────────────────
NAMESPACE="transaction-aggregation"
API_IMAGE="transactionaggregationapi:latest"
UI_IMAGE="transactionaggregationui:latest"
ROLLOUT_TIMEOUT="180s"

# Detect WSL2 so we can surface Windows-specific hosts-file guidance
IS_WSL=false
if grep -qi "microsoft" /proc/version 2>/dev/null; then
  IS_WSL=true
fi

# ── Argument parsing ──────────────────────────────────────────────────────────
OPT_UI=false
OPT_DEV_TOOLS=false
OPT_MONITORING=false
OPT_SKIP_HOSTS=false
OPT_TEARDOWN=false

for arg in "$@"; do
  case "$arg" in
    --ui)          OPT_UI=true ;;
    --dev-tools)   OPT_DEV_TOOLS=true ;;
    --monitoring)  OPT_MONITORING=true ;;
    --skip-hosts)  OPT_SKIP_HOSTS=true ;;
    --teardown)    OPT_TEARDOWN=true ;;
    --help|-h)
      echo "Usage: $(basename "$0") [OPTIONS]"
      echo ""
      echo "Options:"
      echo "  --ui           Also deploy the Blazor WASM UI"
      echo "  --dev-tools    Also deploy pgAdmin and Redis Commander"
      echo "  --monitoring   Also deploy Prometheus and Grafana"
      echo "  --skip-hosts   Skip updating /etc/hosts"
      echo "  --teardown     Delete namespace '$NAMESPACE' and all its data"
      echo "  --help         Show this help"
      exit 0
      ;;
    *)
      log_error "Unknown argument: $arg  (run with --help for usage)"
      exit 1
      ;;
  esac
done

# ── Resolve K8S directory (script must live next to k8s/) ────────────────────
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
K8S="$SCRIPT_DIR/k8s"

if [[ ! -d "$K8S" ]]; then
  log_error "k8s/ directory not found at: $K8S"
  log_error "Run this script from the solution root (the folder with docker-compose.yml)."
  exit 1
fi

# ── Teardown shortcut ─────────────────────────────────────────────────────────
if $OPT_TEARDOWN; then
  log_step "Teardown — deleting namespace '$NAMESPACE'"
  if kubectl get namespace "$NAMESPACE" &>/dev/null 2>&1; then
    kubectl delete namespace "$NAMESPACE"
    log_ok "Namespace '$NAMESPACE' deleted (all pods, services, PVCs and data removed)"
  else
    log_warn "Namespace '$NAMESPACE' does not exist — nothing to do"
  fi
  exit 0
fi

# ── Dynamic step counter ──────────────────────────────────────────────────────
STEP=0
step() { STEP=$((STEP + 1)); log_step "Step $STEP  — $*"; }

# =============================================================================
echo ""
echo -e "${BOLD}${CYAN}┌─────────────────────────────────────────────────────┐${NC}"
echo -e "${BOLD}${CYAN}│   Transaction Aggregation — Kubernetes Deployment   │${NC}"
echo -e "${BOLD}${CYAN}└─────────────────────────────────────────────────────┘${NC}"
echo ""

# ── Pre-flight checks ─────────────────────────────────────────────────────────
log_step "Pre-flight checks"

if ! command -v kubectl &>/dev/null; then
  log_error "kubectl not found."
  log_error "Install Rancher Desktop (https://rancherdesktop.io) — it ships kubectl."
  exit 1
fi
log_ok "kubectl found"

if ! kubectl cluster-info &>/dev/null 2>&1; then
  log_error "Cannot reach the Kubernetes cluster."
  log_error "Make sure Rancher Desktop is open and the cluster is running."
  exit 1
fi
CURRENT_CTX="$(kubectl config current-context 2>/dev/null || echo 'unknown')"
log_ok "Cluster is reachable  (context: ${CURRENT_CTX})"

if command -v nerdctl &>/dev/null; then
  if nerdctl --namespace k8s.io images 2>/dev/null | grep -q "transactionaggregationapi"; then
    log_ok "API image present:  $API_IMAGE"
  else
    log_error "API image NOT found in the k8s.io namespace: $API_IMAGE"
    log_error "Build it first, then re-run this script:"
    log_error "  nerdctl --namespace k8s.io build -t $API_IMAGE -f TransactionAggregationAPI/Dockerfile ."
    exit 1
  fi

  if $OPT_UI; then
    if nerdctl --namespace k8s.io images 2>/dev/null | grep -q "transactionaggregationui"; then
      log_ok "UI  image present:  $UI_IMAGE"
    else
      log_error "UI image NOT found in the k8s.io namespace: $UI_IMAGE"
      log_error "Build it first, then re-run this script:"
      log_error "  nerdctl --namespace k8s.io build -t $UI_IMAGE -f TransactionAggregationUI/Dockerfile ."
      exit 1
    fi
  fi
else
  log_warn "nerdctl not found — cannot verify images are present in the k8s.io namespace."
  log_warn "If a pod fails with ErrImagePull, build the images first:"
  log_warn "  nerdctl --namespace k8s.io build -t $API_IMAGE -f TransactionAggregationAPI/Dockerfile ."
  if $OPT_UI; then
    log_warn "  nerdctl --namespace k8s.io build -t $UI_IMAGE -f TransactionAggregationUI/Dockerfile ."
  fi
fi

if grep -q "CHANGE_ME_BASE64_ENCODED" "$K8S/secrets.yaml" 2>/dev/null; then
  log_error "k8s/secrets.yaml still contains CHANGE_ME_BASE64_ENCODED placeholders."
  log_error ""
  log_error "Generate real values and paste them into the file:"
  log_error "  postgres-password:  echo -n 'YourPassword123!'  | base64"
  log_error "  jwt-secret:         openssl rand -hex 32        | base64"
  log_error "  pgadmin-password:   echo -n 'YourAdminPass!'    | base64"
  exit 1
fi
log_ok "secrets.yaml has been populated"

# ── Step 1: Namespace ─────────────────────────────────────────────────────────
step "Namespace"
kubectl apply -f "$K8S/namespace.yaml"
log_ok "Namespace '$NAMESPACE' ready"

# ── Step 2: Secrets and ConfigMaps ───────────────────────────────────────────
step "Secrets and ConfigMaps"
kubectl apply -f "$K8S/secrets.yaml"
log_ok "Secrets applied"
kubectl apply -f "$K8S/configmap.yaml"
log_ok "API ConfigMap applied"
if $OPT_UI; then
  kubectl apply -f "$K8S/ui/configmap.yaml"
  log_ok "UI  ConfigMap applied  (API_UPSTREAM → http://transaction-api)"
fi

# ── Step 3: Data stores ──────────────────────────────────────────────────────
step "Data stores  (PostgreSQL · Redis · Seq)"
kubectl apply -f "$K8S/postgres/"
kubectl apply -f "$K8S/redis/"
kubectl apply -f "$K8S/seq/"

log_info "Waiting for PostgreSQL to be ready…"
kubectl rollout status statefulset/postgres -n "$NAMESPACE" --timeout="$ROLLOUT_TIMEOUT"
log_ok "PostgreSQL is ready"

log_info "Waiting for Redis to be ready…"
kubectl rollout status statefulset/redis -n "$NAMESPACE" --timeout="$ROLLOUT_TIMEOUT"
log_ok "Redis is ready"

# ── Step 4: API ───────────────────────────────────────────────────────────────
# NOTE: EF Core migrations run automatically inside the API on startup
# (Program.cs → ApplyMigrationsAsync). k8s/api/migration-job.yaml exists if
# you ever need to run migrations as a separate pre-deploy Job instead.
step "API  (service · deployment · ingress · HPA · PDB)"
kubectl apply -f "$K8S/api/service.yaml"
kubectl apply -f "$K8S/api/deployment.yaml"
kubectl apply -f "$K8S/api/ingress.yaml"
kubectl apply -f "$K8S/api/hpa.yaml"
kubectl apply -f "$K8S/api/pdb.yaml"

log_info "Waiting for API replicas to pass the readiness probe (/health)…"
kubectl rollout status deployment/transaction-api -n "$NAMESPACE" --timeout="$ROLLOUT_TIMEOUT"
log_ok "API is running"

# ── Step 5: UI (optional) ─────────────────────────────────────────────────────
if $OPT_UI; then
  step "UI  (deployment · service · ingress)"
  kubectl apply -f "$K8S/ui/deployment.yaml"
  kubectl apply -f "$K8S/ui/service.yaml"
  kubectl apply -f "$K8S/ui/ingress.yaml"
  log_info "Waiting for UI replicas to be ready…"
  kubectl rollout status deployment/transaction-ui -n "$NAMESPACE" --timeout=60s
  log_ok "UI is running"
else
  log_info "UI skipped — pass --ui to deploy the Blazor WASM frontend"
fi

# ── Step 6: Network policies ─────────────────────────────────────────────────
step "Network policies"
kubectl apply -f "$K8S/network-policy.yaml"
log_ok "Network policies applied"
log_info "(Enforced only when your CNI supports NetworkPolicy: Calico, Cilium, Canal)"

# ── Step 7: Dev tools (optional) ─────────────────────────────────────────────
if $OPT_DEV_TOOLS; then
  step "Dev tools  (pgAdmin · Redis Commander)"
  kubectl apply -f "$K8S/dev-tools/pgadmin/"
  kubectl apply -f "$K8S/dev-tools/redis-commander/"
  log_ok "Dev tools deployed"
else
  log_info "Dev tools skipped — pass --dev-tools to enable"
fi

# ── Step 8: Monitoring (optional) ────────────────────────────────────────────
if $OPT_MONITORING; then
  step "Monitoring  (Prometheus · Grafana)"

  # Apply Prometheus RBAC first so the ServiceAccount exists before the Deployment
  kubectl apply -f "$K8S/monitoring/prometheus/serviceaccount.yaml"
  kubectl apply -f "$K8S/monitoring/prometheus/clusterrole.yaml"
  kubectl apply -f "$K8S/monitoring/prometheus/clusterrolebinding.yaml"
  kubectl apply -f "$K8S/monitoring/prometheus/configmap.yaml"
  kubectl apply -f "$K8S/monitoring/prometheus/deployment.yaml"
  kubectl apply -f "$K8S/monitoring/prometheus/service.yaml"
  kubectl apply -f "$K8S/monitoring/prometheus/ingress.yaml"

  kubectl apply -f "$K8S/monitoring/grafana/configmap-provisioning.yaml"
  kubectl apply -f "$K8S/monitoring/grafana/configmap-dashboards.yaml"
  kubectl apply -f "$K8S/monitoring/grafana/pvc.yaml"
  kubectl apply -f "$K8S/monitoring/grafana/deployment.yaml"
  kubectl apply -f "$K8S/monitoring/grafana/service.yaml"
  kubectl apply -f "$K8S/monitoring/grafana/ingress.yaml"

  log_info "Waiting for Prometheus to be ready…"
  kubectl rollout status deployment/prometheus -n "$NAMESPACE" --timeout=90s || true
  log_ok "Prometheus is ready"

  log_info "Waiting for Grafana to be ready…"
  kubectl rollout status deployment/grafana -n "$NAMESPACE" --timeout=90s || true
  log_ok "Grafana is ready"

  sleep 3
  # Use 127.0.0.1 directly — the hostname isn't in /etc/hosts yet (step 9)
  if curl -sf --max-time 5 "http://127.0.0.1/api/health" \
       -H "Host: grafana.transaction.local" > /dev/null 2>&1; then
    log_ok "Grafana health check passed  →  http://grafana.transaction.local"
  else
    log_info "Grafana ingress not responding yet — Traefik may still be syncing."
    log_info "Port-forward to verify the pod is healthy:"
    log_info "  kubectl port-forward svc/grafana 3000:80 -n $NAMESPACE"
    log_info "  curl http://localhost:3000/api/health"
  fi

  log_ok "Monitoring stack deployed"
else
  log_info "Monitoring skipped — pass --monitoring to deploy Prometheus + Grafana"
fi

# ── Step 9: /etc/hosts ───────────────────────────────────────────────────────
HOSTS_ENTRIES=(
  "127.0.0.1  api.transaction.local"
)

if $OPT_UI;         then HOSTS_ENTRIES+=("127.0.0.1  ui.transaction.local"); fi
if $OPT_DEV_TOOLS;  then HOSTS_ENTRIES+=("127.0.0.1  pgadmin.transaction.local"
                                          "127.0.0.1  redis-commander.transaction.local"); fi
if $OPT_MONITORING; then HOSTS_ENTRIES+=("127.0.0.1  prometheus.transaction.local"
                                          "127.0.0.1  grafana.transaction.local"); fi

if $OPT_SKIP_HOSTS; then
  log_info "/etc/hosts update skipped — pass --skip-hosts to skip"
else
  step "/etc/hosts"
  ADDED=0
  for ENTRY in "${HOSTS_ENTRIES[@]}"; do
    HOSTNAME="$(echo "$ENTRY" | awk '{print $2}')"
    if grep -qF "$HOSTNAME" /etc/hosts 2>/dev/null; then
      log_info "Already present: $HOSTNAME"
    else
      echo "$ENTRY" | sudo tee -a /etc/hosts > /dev/null
      log_ok "Added: $ENTRY"
      ADDED=$((ADDED + 1))
    fi
  done
  [[ $ADDED -eq 0 ]] && log_ok "All hostnames were already in /etc/hosts"

  # WSL2: the entries above only apply inside WSL2. Windows browsers read a
  # separate file. Print instructions for any entries that are missing there.
  if $IS_WSL; then
    WIN_HOSTS="/mnt/c/Windows/System32/drivers/etc/hosts"
    MISSING_WIN=()
    for ENTRY in "${HOSTS_ENTRIES[@]}"; do
      HOSTNAME="$(echo "$ENTRY" | awk '{print $2}')"
      if ! grep -qF "$HOSTNAME" "$WIN_HOSTS" 2>/dev/null; then
        MISSING_WIN+=("$ENTRY")
      fi
    done
    if [[ ${#MISSING_WIN[@]} -gt 0 ]]; then
      echo ""
      log_warn "WSL2 detected — Windows browsers use a separate hosts file."
      log_warn "Add the missing entries to $WIN_HOSTS"
      log_warn "(requires an elevated PowerShell / notepad as Administrator):"
      for ENTRY in "${MISSING_WIN[@]}"; do
        log_warn "  $ENTRY"
      done
    fi
  fi
fi

# ── Final verification ────────────────────────────────────────────────────────
echo ""
echo -e "${BOLD}─────────────────────  Pod status  ───────────────────────${NC}"
kubectl get pods -n "$NAMESPACE" --sort-by='.metadata.name' 2>/dev/null || true

echo ""
echo -e "${BOLD}─────────────────────  Ingresses  ────────────────────────${NC}"
kubectl get ingress -n "$NAMESPACE" 2>/dev/null || true

echo ""
log_info "Running API health check…"
sleep 2
if curl -sf --max-time 5 "http://api.transaction.local/health" > /dev/null 2>&1; then
  log_ok "Health check passed  →  http://api.transaction.local/health"
else
  log_warn "Health check via ingress did not respond yet."
  log_warn "Traefik may still be picking up the Ingress — wait ~10 s and try:"
  log_warn "  curl http://api.transaction.local/health"
  log_warn "Or bypass with port-forward:"
  log_warn "  kubectl port-forward svc/transaction-api 8080:80 -n $NAMESPACE"
  log_warn "  curl http://localhost:8080/health"
fi

# ── Summary ───────────────────────────────────────────────────────────────────
echo ""
echo -e "${BOLD}${GREEN}┌─────────────────────────────────────────────────────┐${NC}"
echo -e "${BOLD}${GREEN}│              Deployment complete ✔                  │${NC}"
echo -e "${BOLD}${GREEN}└─────────────────────────────────────────────────────┘${NC}"
echo ""
echo -e "${BOLD}  Service URLs${NC}"
echo -e "  ${GREEN}→${NC}  API              http://api.transaction.local"
echo -e "  ${GREEN}→${NC}  Health           http://api.transaction.local/health"
echo -e "  ${GREEN}→${NC}  Metrics          http://api.transaction.local/metrics"
if $OPT_UI; then
  echo -e "  ${GREEN}→${NC}  UI (start here)  http://ui.transaction.local"
fi
if $OPT_DEV_TOOLS; then
  echo -e "  ${GREEN}→${NC}  pgAdmin          http://pgadmin.transaction.local"
  echo -e "  ${GREEN}→${NC}  Redis Commander  http://redis-commander.transaction.local"
fi
if $OPT_MONITORING; then
  echo -e "  ${GREEN}→${NC}  Prometheus       http://prometheus.transaction.local"
  echo -e "  ${GREEN}→${NC}  Grafana          http://grafana.transaction.local  (admin/admin)"
fi

echo ""
echo -e "${BOLD}  Seed credentials${NC}  (pre-loaded test accounts)"
echo -e "  Email    thabo.mokoena@example.co.za  (or any seeded user)"
echo -e "  Password Test@12345"

echo ""
echo -e "${BOLD}  Port-forward shortcuts${NC}"
echo -e "  ${BLUE}kubectl port-forward svc/transaction-api 8080:80   -n $NAMESPACE${NC}"
if $OPT_UI; then
  echo -e "  ${BLUE}kubectl port-forward svc/transaction-ui  7200:80   -n $NAMESPACE${NC}"
fi
if $OPT_MONITORING; then
  echo -e "  ${BLUE}kubectl port-forward svc/prometheus      9090:9090 -n $NAMESPACE${NC}"
  echo -e "  ${BLUE}kubectl port-forward svc/grafana         3000:80   -n $NAMESPACE${NC}"
fi

echo ""
echo -e "${BOLD}  Useful commands${NC}"
echo -e "  ${BLUE}kubectl get pods    -n $NAMESPACE${NC}"
echo -e "  ${BLUE}kubectl get events  -n $NAMESPACE --sort-by='.lastTimestamp'${NC}"
echo -e "  ${BLUE}kubectl logs -f deployment/transaction-api -n $NAMESPACE${NC}"
if $OPT_UI;         then echo -e "  ${BLUE}kubectl logs -f deployment/transaction-ui  -n $NAMESPACE${NC}"; fi
if $OPT_MONITORING; then
  echo -e "  ${BLUE}kubectl logs -f deployment/prometheus      -n $NAMESPACE${NC}"
  echo -e "  ${BLUE}kubectl logs -f deployment/grafana         -n $NAMESPACE${NC}"
fi
echo ""
echo -e "  To remove everything:"
echo -e "  ${BLUE}./$(basename "$0") --teardown${NC}"
echo ""
