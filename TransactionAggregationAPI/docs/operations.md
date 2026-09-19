# Operations runbooks

Instructions.md section 23 lists `operations.md` as a required doc; the
production readiness checklist flagged it as **not written** — the threat
model and [failure-scenarios.md](failure-scenarios.md) cover detection and
mitigation *conceptually*, but an on-call engineer needs the actual commands,
not a description of the mechanism. Every command below is grounded in the
real manifests in `k8s/` and the actual options/endpoints in the codebase —
nothing here is aspirational. Where this repo genuinely lacks something an
operator would need (a versioned image tag to roll back to, an Alertmanager
to page on), that's called out explicitly rather than glossed over, per
[production-readiness-checklist.md](production-readiness-checklist.md)'s own
"evidence, not claims" standard.

All commands assume the `transaction-aggregation` namespace and the manual
`kubectl` deployment path documented in [`k8s/README.md`](../k8s/README.md)
(there is no working Helm chart yet — see
[ADR discussion](adr/README.md) and failure-scenarios.md scenario 14).

---

## 1. Provider (bank aggregator) outage

**Symptom**: bank-link initiation/completion requests failing or timing out;
`HttpBankAggregatorClient` calls erroring in logs/traces.

1. Confirm it's the provider, not us:
   ```bash
   kubectl logs -n transaction-aggregation -l app.kubernetes.io/name=transaction-api --tail=200 | grep -i "BankAggregator\|circuit"
   ```
   Look for the standard resilience handler's circuit-breaker messages
   (`AddStandardResilienceHandler()` in `ServiceDefaults/Extensions.cs`) —
   if the circuit is open, retries have already stopped locally; this is
   expected behavior, not a bug.
2. Check `/health` — this failure must **not** affect readiness (there is no
   dependency on the provider in the health check registration in
   `AddDefaultHealthChecks`). If pods are being pulled out of rotation, that
   is itself a bug, not expected provider-outage behavior.
3. No manual intervention un-trips the circuit breaker — it self-probes
   (half-open) once its break duration elapses. There is nothing to restart.
4. Existing linked accounts/transactions are unaffected (failure-scenarios.md
   scenario 3) — only new bank-link operations fail. No customer-facing
   incident communication needed beyond "bank linking is temporarily
   unavailable."
5. **Gap, not yet built**: no alert fires automatically when the circuit
   trips. Watch application logs directly (Seq: `kubectl port-forward -n
   transaction-aggregation svc/seq 8080:80`) until the alerting story in
   `production-readiness-checklist.md`'s Observability section is closed.

---

## 2. Database (PostgreSQL) unavailable

**Symptom**: `/health` reports `Unhealthy`; pods pulled out of Service
rotation; most API requests failing.

1. Check the StatefulSet and pod directly:
   ```bash
   kubectl get pods -n transaction-aggregation -l app.kubernetes.io/name=postgres
   kubectl describe pod postgres-0 -n transaction-aggregation
   kubectl logs postgres-0 -n transaction-aggregation --tail=200
   ```
2. Common causes given this deployment's shape (single-replica StatefulSet,
   `k8s/postgres/statefulset.yaml` — there is no HA here by design, see the
   file's own header comment):
   - **PVC full or unbound**: `kubectl get pvc -n transaction-aggregation` —
     the claim is `postgres-data-postgres-0` against the `local-path`
     StorageClass. A full disk on the underlying node is the most likely
     single-node-cluster cause.
   - **Node down**: since this isn't HA, a node failure taking `postgres-0`
     with it means downtime until the node (or PV, if using a provisioner
     that supports reattachment) recovers. There is no automatic failover —
     this is the accepted trade-off documented in the StatefulSet's own
     comment and in `production-readiness-checklist.md` (backup/restore and
     DR targets are still open items).
   - **OOMKilled**: `kubectl describe pod postgres-0` shows `Last State:
     Terminated, Reason: OOMKilled` if the 1Gi memory limit was hit.
3. Recovery is automatic once Postgres is reachable again — `EnableRetryOnFailure`
   (Npgsql, `maxRetryCount: 5`) handles transient reconnects, and the
   readiness probe returns pods to rotation with no manual step
   (failure-scenarios.md scenario 1).
4. **If the PVC/PV itself is lost** (not just the pod): there is currently
   no tested restore procedure in this repo — `pg_dump`/`pg_restore` against
   the running instance works mechanically:
   ```bash
   # Backup (run before any risky operation, e.g. a manual schema fix):
   kubectl exec -n transaction-aggregation postgres-0 -- \
     pg_dump -U postgres -Fc transactiondb > transactiondb-$(date +%Y%m%d-%H%M%S).dump

   # Restore into a fresh instance:
   kubectl exec -i -n transaction-aggregation postgres-0 -- \
     pg_restore -U postgres -d transactiondb --clean --if-exists < transactiondb-<timestamp>.dump
   ```
   but this has **not been executed and verified** against this schema (the
   `Integration/Postgres` Testcontainers suite tests migrations and
   inbox/outbox behavior, not backup/restore). Treat the commands above as a
   starting point, not a verified procedure, until someone actually runs a
   restore drill — see the "Backup/restore tested" row in
   `production-readiness-checklist.md`.

---

## 3. Redis unavailable

**Symptom**: `/health` reports `Degraded` (not `Unhealthy` — see
[ADR-0005](adr/0005-redis-cache-only.md)); pods **stay in rotation**; slower
reads; bank-link initiation specifically fails (it depends on Redis for OAuth
state).

1. This is graceful by design — confirm nothing else is wrong before
   "fixing" anything:
   ```bash
   kubectl get pods -n transaction-aggregation -l app.kubernetes.io/name=redis
   kubectl logs redis-0 -n transaction-aggregation --tail=100
   ```
2. No action is required for the API to keep serving most traffic — caching
   fails open (cache miss → DB read) and the rate limiter fails open
   (`AllowRequestOnRedisFailure = true`).
3. Restart Redis the same way as any single-replica StatefulSet pod issue
   (`kubectl delete pod redis-0 -n transaction-aggregation` lets the
   StatefulSet controller recreate it against the existing PVC — data is
   preserved if only the process crashed, lost if the PVC itself is gone,
   which is acceptable since Redis is never the source of truth here).
4. If Data Protection keys were being persisted to Redis and it's
   unreachable at pod *startup* (not mid-run), the app falls back to
   `UseEphemeralDataProtectionProvider()` automatically (failure-scenarios.md
   scenario 2) — no manual step, but this directly affects bank-link token
   encryption: `BankLinkCredentialProtector` (`TransactionAggregation.Infrastructure/
   Authentication/`) is built on `IDataProtectionProvider.CreateProtector("BankLink.Tokens.v1")`,
   not a separate mechanism. Any bank-link token protected during an
   ephemeral-key episode becomes **unrecoverable** once the pod restarts or
   Redis recovers and the app reverts to persisted keys — `Unprotect` will
   throw. If this happens, the affected customer(s) will need to re-link
   their bank account; there is no way to recover the old token.

---

## 4. Poison messages / dead-letter pileup

**Symptom**: `inbox_messages_dead_lettered_total` or
`outbox_messages_dead_lettered_total` (Prometheus counters, see
[failure-scenarios.md](failure-scenarios.md) scenario 17) climbing instead of
staying flat.

1. Query current dead-letter counts by label (source/type) directly against
   Prometheus (`kubectl port-forward -n transaction-aggregation svc/prometheus
   9090:9090`, then query `inbox_messages_dead_lettered_total` /
   `outbox_messages_dead_lettered_total` in the UI), or via Grafana if the
   provisioned dashboard is deployed.
2. Find the actual dead-lettered rows and their `LastError`:
   ```sql
   -- via: kubectl exec -it -n transaction-aggregation postgres-0 -- psql -U postgres -d transactiondb
   SELECT "Id", "SourceName", "Attempts", "LastError", "ReceivedAt"
   FROM "InboxMessages" WHERE "Status" = 3 -- DeadLettered (InboxMessageStatus enum: Pending=0, Processing=1, Processed=2, DeadLettered=3)
   ORDER BY "ReceivedAt" DESC LIMIT 20;

   SELECT "Id", "Type", "Attempts", "LastError", "OccurredAt"
   FROM "OutboxMessages" WHERE "Status" = 3 -- DeadLettered (OutboxMessageStatus enum: same ordering)
   ORDER BY "OccurredAt" DESC LIMIT 20;
   ```
   Re-check `TransactionAggregation.Domain/Inbox/InboxMessageStatus.cs` /
   `.../Outbox/OutboxMessageStatus.cs` if either enum is ever reordered —
   these queries hardcode the numeric value, and EF Core stores enums as
   plain integers here, not as strings.
3. A dead-lettered message **does not block other messages** — both
   dispatchers poll `Status = Pending`, not FIFO (failure-scenarios.md
   scenario 9/17). There is no urgency to "unblock the queue"; the urgency is
   understanding *why* messages are failing deterministically.
4. Common causes: a genuinely malformed payload from a provider (check
   `LastError`), a downstream dependency change that broke a handler (e.g. a
   schema change to `Transactions` that an in-flight outbox message's stale
   payload shape no longer matches), or `MaxAttempts` (`InboxOptions`/
   `OutboxOptions`, default 5) being too low for a transient issue that
   needed more retries.
5. **Reprocessing a dead-lettered message**: there is no built-in
   "requeue" API. Manually reset it to `Pending` if the underlying cause is
   fixed:
   ```sql
   UPDATE "OutboxMessages" SET "Status" = 0, "Attempts" = 0, "NextAttemptAt" = NULL
   WHERE "Id" = '<message-id>'; -- 0 = Pending
   ```
   Do this deliberately, one message at a time, after confirming the root
   cause is actually fixed — resetting a message that fails the same way
   just burns another `MaxAttempts` cycle.

---

## 5. Database migration Job fails

**Symptom**: `kubectl wait --for=condition=complete job/db-migrate` times out
or the Job shows `Failed`.

1. ```bash
   kubectl logs -n transaction-aggregation job/db-migrate
   kubectl describe job/db-migrate -n transaction-aggregation
   ```
2. Confirm the Job actually has the full config set it needs — this was a
   real, previously-shipped bug (failure-scenarios.md scenario 14): the Job
   must carry `envFrom: transaction-api-config` plus explicit
   `ConnectionStrings__seq`/`ConnectionStrings__redis`, or it crashes before
   attempting a single migration with an unrelated-looking error
   (`Unable to add a Seq health check because the 'ServerUrl' setting is
   missing`). If you see that specific error, this is a config regression,
   not a migration problem.
3. Recovery is forward-only (EF Core migrations aren't transactional across
   all DDL on every database) — fix the migration or the data issue that's
   blocking it, then re-run:
   ```bash
   kubectl delete job db-migrate -n transaction-aggregation
   kubectl apply -f k8s/api/migration-job.yaml -n transaction-aggregation
   kubectl wait --for=condition=complete job/db-migrate -n transaction-aggregation --timeout=120s
   ```
4. **Do not** apply `k8s/api/deployment.yaml` until the Job completes —
   nothing currently enforces this ordering automatically (no real Helm
   chart to make it a pre-install hook; see `k8s/README.md` and
   ADR discussion). The previous version keeps serving traffic unaffected
   as long as you don't skip this step manually.

---

## 6. Rolling back a bad deployment

**Read this before an incident, not during one** — this repo has a real gap
here, described honestly rather than papered over:

`k8s/api/deployment.yaml` pins `image: transactionaggregationapi:latest`.
Kubernetes' own rollback mechanism (`kubectl rollout undo
deployment/transaction-api`) rolls back to the *previous ReplicaSet's pod
template* — but if that previous template also said `:latest`, a rollback
re-pulls whatever `:latest` currently resolves to (with `imagePullPolicy:
IfNotPresent`, likely nothing changes at all, since the locally-cached image
won't be re-pulled). **`kubectl rollout undo` will not reliably restore the
previous code** under this manifest as it stands today. This is a real
must-have gap for anyone relying on this repo's k8s manifests for an actual
rollback — production deployments need a manifest pinned to an immutable,
versioned tag (e.g. a git SHA or semver), not `:latest`.

Until that's fixed, the actually-reliable rollback procedure is:

1. Identify the last known-good image tag/build (from CI history or your own
   build log — there is currently no automated deploy step recording this;
   see the "Automated deployment to staging/production" row in
   `production-readiness-checklist.md`).
2. Rebuild or re-pull that specific version, tag it explicitly, and edit the
   image directly:
   ```bash
   kubectl set image deployment/transaction-api \
     transaction-api=<registry>/transactionaggregationapi:<known-good-tag> \
     -n transaction-aggregation
   kubectl rollout status deployment/transaction-api -n transaction-aggregation
   ```
3. If the bad deploy included a database migration that isn't safely
   backward-compatible with the old code, rolling back the Deployment alone
   is not sufficient — this is exactly why ADR guidance favors expand/contract
   migrations (Instructions.md section 14). Check whether the migration
   applied by `db-migrate` for this release is additive-only before assuming
   an app rollback is enough; a destructive/renaming migration needs a
   forward-fix, not a rollback.
4. The Deployment's `maxUnavailable: 0` rolling-update strategy means the
   rollback itself is zero-downtime, same as a normal deploy — old pods stay
   serving traffic until new ones (running the known-good image) pass their
   readiness probe.

---

## 7. Unauthorized/anomalous access

See [threat-model.md](threat-model.md) section 2 and
`AccountApiSecurityTests` for what's already tested (IDOR/BOLA return 404,
not a leak). For a suspected credential compromise or active abuse:

1. Revoke the specific user's tokens/sessions at the Keycloak level (this
   system delegates identity entirely to Keycloak — there is no local
   session store to purge).
2. Webhook source compromise: rotate the affected source's API key —
   `POST /api/v1/admin/webhook-sources/{id}/rotate` (see
   `WebhookSourceEndpoints.cs`) invalidates the old key immediately.
3. There is no automated anomaly detection or IP-based blocking in this
   repo today beyond the Redis-backed fixed-window rate limiter — a
   sustained attack from a single identity is throttled, not blocked
   outright.
