#!/usr/bin/env bash
# Post-deployment smoke test: proves a running API is up, gated, and hardened — without
# credentials, so it can run against any environment (CI stack, staging, production).
#   usage: scripts/smoke-test.sh <base-url>        e.g. scripts/smoke-test.sh http://localhost:5001
set -euo pipefail

BASE="${1:?usage: smoke-test.sh <base-url>}"
BASE="${BASE%/}"
failures=0

pass() { printf '  \033[32mPASS\033[0m %s\n' "$1"; }
fail() { printf '  \033[31mFAIL\033[0m %s\n' "$1"; failures=$((failures + 1)); }

check_status() { # name, expected status, curl args...
  local name="$1" expected="$2"; shift 2
  local status
  status=$(curl -s -o /dev/null -w '%{http_code}' --max-time 10 "$@") || status="000"
  [[ "$status" == "$expected" ]] && pass "$name ($status)" || fail "$name: expected $expected, got $status"
}

echo "Smoke testing $BASE"

check_status "liveness" 200 "$BASE/liveness"
check_status "readiness (Postgres reachable)" 200 "$BASE/readiness"

# Authentication is enforced, and errors use the ProblemDetails contract.
check_status "transactions API requires a token" 401 "$BASE/api/v1/transactions"
content_type=$(curl -s -o /dev/null -w '%{content_type}' --max-time 10 "$BASE/api/v1/transactions")
[[ "$content_type" == application/problem+json* ]] && pass "401 body is ProblemDetails" || fail "401 content type was '$content_type'"

check_status "webhook requires an API key" 401 -X POST -H 'Content-Type: application/json' -d '{}' "$BASE/api/v1/webhooks/bank-aggregator/transactions"

# Security headers on API responses.
headers=$(curl -s -D - -o /dev/null --max-time 10 "$BASE/api/v1/transactions" | tr -d '\r')
grep -qi '^x-content-type-options: nosniff' <<<"$headers" && pass "X-Content-Type-Options" || fail "X-Content-Type-Options missing"
grep -qi "^content-security-policy: default-src 'none'" <<<"$headers" && pass "Content-Security-Policy" || fail "Content-Security-Policy missing"

# Metrics must not be published on the public port.
if curl -s --max-time 10 "$BASE/metrics" | grep -q '^# HELP'; then
  fail "/metrics is exposed on the public port"
else
  pass "/metrics not exposed publicly"
fi

if (( failures > 0 )); then
  echo "Smoke test FAILED ($failures check(s))"
  exit 1
fi
echo "Smoke test passed"
