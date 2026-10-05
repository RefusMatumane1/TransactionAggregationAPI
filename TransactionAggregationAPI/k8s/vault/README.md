# Secrets from Vault

Each host reads its secrets from a `secrets.json` merged over `appsettings.json` at start-up
(`AddSecretsFile`). Locally that file sits next to the project and is git-ignored. In a
cluster the Vault Agent Injector renders a file with the same structure, and `Secrets__FilePath`
points the host at it. The application reads a file either way; it has no Vault client and no
Vault token.

## One-time setup

```bash
# Policies: each workload may read only its own document.
vault policy write transaction-api - <<'EOF'
path "secret/data/transaction-aggregation/api" { capabilities = ["read"] }
EOF
vault policy write transaction-worker - <<'EOF'
path "secret/data/transaction-aggregation/worker" { capabilities = ["read"] }
EOF

# Kubernetes auth: bind each service account to its policy.
vault write auth/kubernetes/role/transaction-api \
  bound_service_account_names=default bound_service_account_namespaces=transaction-aggregation \
  policies=transaction-api ttl=1h
vault write auth/kubernetes/role/transaction-worker \
  bound_service_account_names=default bound_service_account_namespaces=transaction-aggregation \
  policies=transaction-worker ttl=1h
```

## The documents

Keys use `:` as the section separator, exactly as configuration keys do, so a flat KV document
renders into a file the JSON configuration provider reads like a nested `secrets.json`:

```bash
vault kv put secret/transaction-aggregation/api \
  "ConnectionStrings:transactiondb=Host=pgbouncer;Port=6432;Database=transactiondb;Username=tagg_app;Password=<app-db-password>;Maximum Pool Size=30;No Reset On Close=true"

vault kv put secret/transaction-aggregation/worker \
  "ConnectionStrings:transactiondb=Host=pgbouncer;Port=6432;Database=transactiondb;Username=tagg_app;Password=<app-db-password>;Maximum Pool Size=20;No Reset On Close=true" \
  "NotificationOptions:DuplicateAlertWebhookUrl=<incoming-webhook-url>"
```

Values in the rendered file take precedence over `appsettings.json` but not over environment
variables. The patches therefore delete the Deployment's Secret-based
`ConnectionStrings__transactiondb` and `APP_DB_PASSWORD` entries, so the Vault value is the only
one the host sees. When you move another value into Vault, delete its `env` entry in the patch
the same way (`$patch: delete`); `kubectl patch --local -f k8s/api/deployment.yaml --type
strategic --patch-file k8s/vault/api-patch.yaml -o yaml` shows the result without a cluster.

## Deploy and rotate

`./deploy-k8s.sh --vault` applies the manifests and then `api-patch.yaml` / `worker-patch.yaml`.
When a value changes in Vault, the agent re-renders the file and the host reloads it
(`ReloadOnChange`). A new database password still needs the pods restarted, because pooled
connections were opened with the old one: `kubectl rollout restart deployment/transaction-api
deployment/transaction-worker -n transaction-aggregation`.

If the file has not been rendered, the host refuses to start and names the missing path.
