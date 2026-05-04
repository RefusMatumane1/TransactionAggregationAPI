#!/usr/bin/env bash
# =============================================================================
# deploy-k8s.sh
# Deploy the Transaction Aggregation stack to a local Kubernetes cluster
# (Rancher Desktop / k3s).
#
# Assumes both images are already built into the k8s.io containerd namespace:
#   transactionaggregationapi:latest
#   transactionaggregationui:latest
#
# Usage:
#   ./deploy-k8s.sh                  Deploy everything
#   ./deploy-k8s.sh --dev-tools      Also deploy pgAdmin + Redis Commander
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
MIGRATE_TIMEOUT="120s"

# ── Argument parsing ──────────────────────────────────────────────────────────
OPT_DEV_TOOLS=false
OPT_SKIP_HOSTS=false
OPT_TEARDOWN=false

for arg in "$@"; do
  case "$arg" in
    --dev-tools)   OPT_DEV_TOOLS=true ;;
    --skip-hosts)  OPT_SKIP_HOSTS=true ;;
    --teardown)    OPT_TEARDOWN=true ;;
    --help|-h)
      echo "Usage: $(basename "$0") [OPTIONS]"
      echo ""
      echo "Options:"
      echo "  --dev-tools    Also deploy pgAdmin and Redis Commander"
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

# =============================================================================
echo ""
echo -e "${BOLD}${CYAN}┌─────────────────────────────────────────────────────┐${NC}"
echo -e "${BOLD}${CYAN}│   Transaction Aggregation — Kubernetes Deployment   │${NC}"
echo -e "${BOLD}${CYAN}└─────────────────────────────────────────────────────┘${NC}"
echo ""

# ── Step 0: Pre-flight checks ─────────────────────────────────────────────────
log_step "Pre-flight checks"

# kubectl present?
if ! command -v kubectl &>/dev/null; then
  log_error "kubectl not found."
  log_error "Install Rancher Desktop (https://rancherdesktop.io) — it ships kubectl."
  exit 1
fi
log_ok "kubectl found"

# Cluster reachable?
if ! kubectl cluster-info &>/dev/null 2>&1; then
  log_error "Cannot reach the Kubernetes cluster."
  log_error "Make sure Rancher Desktop is open and the cluster is running."
  exit 1
fi
CURRENT_CTX="$(kubectl config current-context 2>/dev/null || echo 'unknown')"
log_ok "Cluster is reachable  (context: ${CURRENT_CTX})"

# Images present in the k8s.io namespace?
if command -v nerdctl &>/dev/null; then
  if nerdctl --namespace k8s.io images 2>/dev/null | grep -q "transactionaggregationapi"; then
    log_ok "API image present:  $API_IMAGE"
  else
    log_error "API image NOT found in the k8s.io namespace: $API_IMAGE"
    log_error "Build it first, then re-run this script:"
    log_error "  nerdctl --namespace k8s.io build -t $API_IMAGE -f TransactionAggregationAPI/Dockerfile ."
    exit 1
  fi

  if nerdctl --namespace k8s.io images 2>/dev/null | grep -q "transactionaggregationui"; then
    log_ok "UI  image present:  $UI_IMAGE"
  else
    log_error "UI image NOT found in the k8s.io namespace: $UI_IMAGE"
    log_error "Build it first, then re-run this script:"
    log_error "  nerdctl --namespace k8s.io build -t $UI_IMAGE -f TransactionAggregationUI/Dockerfile ."
    exit 1
  fi
else
  log_warn "nerdctl not found — cannot verify images are present in the k8s.io namespace."
  log_warn "If the migration Job fails with ErrImagePull, build the images first:"
  log_warn "  nerdctl --namespace k8s.io build -t $API_IMAGE -f TransactionAggregationAPI/Dockerfile ."
  log_warn "  nerdctl --namespace k8s.io build -t $UI_IMAGE -f TransactionAggregationUI/Dockerfile ."
fi

# secrets.yaml has been populated?
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
log_step "Step 1/9  — Namespace"
kubectl apply -f "$K8S/namespace.yaml"
log_ok "Namespace '$NAMESPACE' ready"

# ── Step 2: Secrets and ConfigMaps ───────────────────────────────────────────
log_step "Step 2/9  — Secrets and ConfigMaps"
kubectl apply -f "$K8S/secrets.yaml"
log_ok "Secrets applied"
kubectl apply -f "$K8S/configmap.yaml"
log_ok "API ConfigMap applied"
kubectl apply -f "$K8S/ui/configmap.yaml"
log_ok "UI  ConfigMap applied  (API_UPSTREAM → http://transaction-api)"

# ── Step 3: Data stores ──────────────────────────────────────────────────────
log_step "Step 3/9  — Data stores  (PostgreSQL, Redis, Seq)"
kubectl apply -f "$K8S/postgres/"
kubectl apply -f "$K8S/redis/"
kubectl apply -f "$K8S/seq/"
log_info "Waiting for PostgreSQL to finish initialising…"
kubectl rollout status statefulset/postgres \
  -n "$NAMESPACE" --timeout="$ROLLOUT_TIMEOUT"
log_ok "PostgreSQL is ready"

# ── Step 4: Database migrations ──────────────────────────────────────────────
# log_step "Step 4/9  — EF Core database migrations"

# # Remove any previously completed Job so we can run a fresh one
# if kubectl get job db-migrate -n "$NAMESPACE" &>/dev/null 2>&1; then
#   log_info "Removing previous migration Job…"
#   kubectl delete job db-migrate -n "$NAMESPACE"
# fi

# kubectl apply -f "$K8S/api/migration-job.yaml"

# log_info "Waiting for the migration Job to complete (up to $MIGRATE_TIMEOUT)…"
# # Give the pod a moment to be scheduled before we start watching
# sleep 3
# kubectl wait --for=condition=complete job/db-migrate \
#   -n "$NAMESPACE" --timeout="$MIGRATE_TIMEOUT"

# log_ok "Migrations applied successfully"
# log_info "Migration output:"
# kubectl logs job/db-migrate -n "$NAMESPACE" 2>/dev/null \
#   | sed 's/^/      /' \
#   || true

# ── Step 5: API ───────────────────────────────────────────────────────────────
log_step "Step 5/9  — API deployment"
kubectl apply -f "$K8S/api/service.yaml"
kubectl apply -f "$K8S/api/deployment.yaml"
kubectl apply -f "$K8S/api/ingress.yaml"
kubectl apply -f "$K8S/api/hpa.yaml"
kubectl apply -f "$K8S/api/pdb.yaml"

log_info "Waiting for both API replicas to pass the readiness probe (/health)…"
kubectl rollout status deployment/transaction-api \
  -n "$NAMESPACE" --timeout="$ROLLOUT_TIMEOUT"
log_ok "API is running"

# ── Step 6: UI ────────────────────────────────────────────────────────────────
# log_step "Step 6/9  — UI deployment  (nginx + Blazor WASM)"
# kubectl apply -f "$K8S/ui/service.yaml"
# kubectl apply -f "$K8S/ui/deployment.yaml"
# kubectl apply -f "$K8S/ui/ingress.yaml"

# log_info "Waiting for UI replicas to be ready…"
# kubectl rollout status deployment/transaction-ui \
#   -n "$NAMESPACE" --timeout=60s
# log_ok "UI is running"

# ── Step 7: Network policies ─────────────────────────────────────────────────
log_step "Step 7/9  — Network policies"
kubectl apply -f "$K8S/network-policy.yaml"
log_ok "Network policies applied"
log_info "(Enforced only when your CNI supports NetworkPolicy: Calico, Cilium, Canal)"

# ── Step 8: Dev tools (optional) ─────────────────────────────────────────────
if $OPT_DEV_TOOLS; then
  log_step "Step 8/9  — Dev tools  (pgAdmin + Redis Commander)"
  kubectl apply -f "$K8S/dev-tools/pgadmin/"
  kubectl apply -f "$K8S/dev-tools/redis-commander/"
  log_ok "Dev tools deployed"
else
  log_step "Step 8/9  — Dev tools  (skipped — pass --dev-tools to enable)"
fi

# ── Step 9: /etc/hosts ───────────────────────────────────────────────────────
HOSTS_ENTRIES=(
  "127.0.0.1  ui.transaction.local"
  "127.0.0.1  api.transaction.local"
  "127.0.0.1  seq.transaction.local"
  "127.0.0.1  pgadmin.transaction.local"
  "127.0.0.1  redis-commander.transaction.local"
)

if $OPT_SKIP_HOSTS; then
  log_step "Step 9/9  — /etc/hosts  (skipped — pass --skip-hosts to skip)"
else
  log_step "Step 9/9  — /etc/hosts"
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
  if [[ $ADDED -eq 0 ]]; then
    log_ok "All hostnames were already in /etc/hosts"
  fi
fi

# ── Final verification ────────────────────────────────────────────────────────
echo ""
echo -e "${BOLD}─────────────────────  Pod status  ───────────────────────${NC}"
kubectl get pods -n "$NAMESPACE" \
  --sort-by='.metadata.name' \
  2>/dev/null || true

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
echo -e "  ${GREEN}→${NC}  UI (start here)  http://ui.transaction.local"
echo -e "  ${GREEN}→${NC}  API              http://api.transaction.local"
echo -e "  ${GREEN}→${NC}  Health           http://api.transaction.local/health"
echo -e "  ${GREEN}→${NC}  Seq logs         http://seq.transaction.local"
if $OPT_DEV_TOOLS; then
  echo -e "  ${GREEN}→${NC}  pgAdmin          http://pgadmin.transaction.local"
  echo -e "  ${GREEN}→${NC}  Redis Commander  http://redis-commander.transaction.local"
fi

echo ""
echo -e "${BOLD}  Seed credentials${NC}  (pre-loaded test accounts)"
echo -e "  Email    thabo.mokoena@example.co.za  (or any seeded user)"
echo -e "  Password Test@12345"

echo ""
echo -e "${BOLD}  Port-forward (if Ingress not working)${NC}"
echo -e "  ${BLUE}kubectl port-forward svc/transaction-ui  7200:80 -n $NAMESPACE${NC}"
echo -e "  ${BLUE}kubectl port-forward svc/transaction-api 8080:80 -n $NAMESPACE${NC}"

echo ""
echo -e "${BOLD}  Useful commands${NC}"
echo -e "  ${BLUE}kubectl get pods    -n $NAMESPACE${NC}"
echo -e "  ${BLUE}kubectl get events  -n $NAMESPACE --sort-by='.lastTimestamp'${NC}"
echo -e "  ${BLUE}kubectl logs -f deployment/transaction-api -n $NAMESPACE${NC}"
echo -e "  ${BLUE}kubectl logs -f deployment/transaction-ui  -n $NAMESPACE${NC}"
echo ""
echo -e "  To remove everything:"
echo -e "  ${BLUE}./$(basename "$0") --teardown${NC}"
echo ""
