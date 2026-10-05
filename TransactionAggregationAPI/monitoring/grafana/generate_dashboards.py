"""Dashboards as code for the Transaction Aggregation Grafana.

Writes monitoring/grafana/dashboards/*.json (docker-compose and Aspire mount that folder) and
k8s/monitoring/grafana/configmap-dashboards.yaml from the same definitions. Run after any change:

    python monitoring/grafana/generate_dashboards.py

Every query uses metric names verified against the live /metrics output of the API and worker
(prometheus-net plus its .NET Meter/EventCounter adapters), not the OpenTelemetry names.
"""
import json, os

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
DS = {"type": "prometheus", "uid": "${datasource}"}

API = 'job="transaction-api"'
WRK = 'job="transaction-worker"'


class Board:
    def __init__(self):
        self.panels, self.y, self.x, self.row_h, self.id = [], 0, 0, 0, 0

    def _place(self, w, h):
        if self.x + w > 24:
            self.y += self.row_h; self.x = 0; self.row_h = 0
        pos = {"h": h, "w": w, "x": self.x, "y": self.y}
        self.x += w; self.row_h = max(self.row_h, h)
        return pos

    def _next_id(self):
        self.id += 1
        return self.id

    def row(self, title):
        if self.x:
            self.y += self.row_h; self.x = 0; self.row_h = 0
        self.panels.append({"type": "row", "title": title, "collapsed": False, "id": self._next_id(),
                            "gridPos": {"h": 1, "w": 24, "x": 0, "y": self.y}, "panels": []})
        self.y += 1

    def _targets(self, exprs):
        out = []
        for i, e in enumerate(exprs):
            expr, legend = e if isinstance(e, tuple) else (e, "")
            out.append({"datasource": DS, "expr": expr, "legendFormat": legend, "refId": chr(65 + i)})
        return out

    def stat(self, title, exprs, unit="short", steps=None, w=4, h=4, desc="", decimals=None, mappings=None):
        steps = steps or [{"color": "green", "value": None}]
        d = {"color": {"mode": "thresholds"}, "mappings": mappings or [], "unit": unit,
             "thresholds": {"mode": "absolute", "steps": steps}}
        if decimals is not None:
            d["decimals"] = decimals
        self.panels.append({
            "type": "stat", "title": title, "description": desc, "id": self._next_id(), "datasource": DS,
            "gridPos": self._place(w, h),
            "fieldConfig": {"defaults": d, "overrides": []},
            "options": {"colorMode": "background", "graphMode": "area", "justifyMode": "auto",
                        "orientation": "auto", "textMode": "auto",
                        "reduceOptions": {"calcs": ["lastNotNull"], "fields": "", "values": False}},
            "targets": self._targets(exprs)})

    def ts(self, title, exprs, unit="short", w=12, h=8, desc="", stack=False, steps=None, bars=False):
        custom = {"drawStyle": "bars" if bars else "line", "lineWidth": 1, "fillOpacity": 60 if bars else 10,
                  "showPoints": "never", "spanNulls": True,
                  "stacking": {"mode": "normal" if stack else "none", "group": "A"}}
        d = {"color": {"mode": "palette-classic"}, "unit": unit, "custom": custom}
        if steps:
            d["thresholds"] = {"mode": "absolute", "steps": steps}
            custom["thresholdsStyle"] = {"mode": "dashed"}
        self.panels.append({
            "type": "timeseries", "title": title, "description": desc, "id": self._next_id(), "datasource": DS,
            "gridPos": self._place(w, h),
            "fieldConfig": {"defaults": d, "overrides": []},
            "options": {"legend": {"displayMode": "table", "placement": "bottom", "calcs": ["mean", "max", "lastNotNull"]},
                        "tooltip": {"mode": "multi", "sort": "desc"}},
            "targets": self._targets(exprs)})

    def table(self, title, expr, w=24, h=6, desc=""):
        self.panels.append({
            "type": "table", "title": title, "description": desc, "id": self._next_id(), "datasource": DS,
            "gridPos": self._place(w, h),
            "fieldConfig": {"defaults": {"custom": {"align": "auto"}}, "overrides": []},
            "options": {"showHeader": True, "footer": {"show": False},
                        "sortBy": [{"displayName": "severity", "desc": False}]},
            "targets": [{"datasource": DS, "expr": expr, "format": "table", "instant": True, "refId": "A"}],
            "transformations": [{"id": "organize", "options": {
                "excludeByName": {"Time": True, "Value": True, "__name__": True, "alertstate": True},
                "indexByName": {"severity": 0, "alertname": 1, "job": 2, "instance": 3}}}]})

    def dashboard(self, uid, title, desc, tags, variables, links):
        return {
            "annotations": {"list": [
                {"builtIn": 1, "datasource": {"type": "grafana", "uid": "-- Grafana --"}, "enable": True,
                 "hide": True, "iconColor": "rgba(0, 211, 255, 1)", "name": "Annotations & Alerts", "type": "dashboard"},
                {"datasource": DS, "enable": True, "iconColor": "red", "name": "Firing alerts",
                 "expr": 'ALERTS{alertstate="firing"}', "step": "60s", "titleFormat": "{{alertname}}",
                 "textFormat": "{{job}} {{instance}}", "useValueForTime": False}]},
            "description": desc, "editable": True, "fiscalYearStartMonth": 0, "graphTooltip": 1, "id": None,
            "links": links, "panels": self.panels, "refresh": "30s", "schemaVersion": 39, "tags": tags,
            "templating": {"list": variables}, "time": {"from": "now-6h", "to": "now"},
            "timepicker": {}, "timezone": "browser", "title": title, "uid": uid, "version": 1}


DS_VAR = {"current": {}, "hide": 0, "includeAll": False, "label": "Data source", "multi": False,
          "name": "datasource", "options": [], "query": "prometheus", "refresh": 1, "regex": "", "type": "datasource"}


def instance_var(job):
    return {"current": {"selected": True, "text": ["All"], "value": ["$__all"]}, "datasource": DS,
            "definition": f'label_values(up{{{job}}}, instance)', "hide": 0, "includeAll": True, "multi": True,
            "label": "Instance", "name": "instance", "options": [],
            "query": {"query": f'label_values(up{{{job}}}, instance)', "refId": "StandardVariableQuery"},
            "refresh": 2, "regex": "", "sort": 1, "type": "query"}


LINKS = [{"asDropdown": False, "icon": "dashboard", "includeVars": False, "keepTime": True, "tags": ["transaction-aggregation"],
          "targetBlank": False, "title": "Transaction Aggregation", "type": "dashboards"}]

RED = [{"color": "green", "value": None}, {"color": "red", "value": 1}]
UPMAP = [{"type": "value", "options": {"0": {"text": "DOWN", "color": "red"}, "1": {"text": "UP", "color": "green"}}}]


def runtime_and_db(b, sel):
    b.row("Database (Npgsql / EF Core)")
    b.ts("DB operation latency", [
        (f'histogram_quantile(0.50, sum by (le) (rate(npgsql_db_client_operation_duration_bucket{{{sel}}}[5m])))', "p50"),
        (f'histogram_quantile(0.95, sum by (le) (rate(npgsql_db_client_operation_duration_bucket{{{sel}}}[5m])))', "p95"),
        (f'histogram_quantile(0.99, sum by (le) (rate(npgsql_db_client_operation_duration_bucket{{{sel}}}[5m])))', "p99")],
        unit="s", w=8, desc="Every command sent to PostgreSQL, measured by Npgsql.")
    b.ts("DB operations / sec", [
        (f'sum(rate(npgsql_db_client_operation_duration_count{{{sel}}}[5m]))', "commands"),
        (f'sum(rate(microsoft_entityframeworkcore_microsoft_entityframeworkcore_queries{{{sel}}}[5m]))', "EF queries"),
        (f'sum(rate(microsoft_entityframeworkcore_microsoft_entityframeworkcore_savechanges{{{sel}}}[5m]))', "EF SaveChanges")],
        unit="ops", w=8)
    b.ts("Connection pool", [
        (f'sum by (instance) (npgsql_db_client_connection_count{{{sel},db_client_connection_state="used"}})', "used {{instance}}"),
        (f'sum by (instance) (npgsql_db_client_connection_count{{{sel},db_client_connection_state="idle"}})', "idle {{instance}}"),
        (f'max by (instance) (npgsql_db_client_connection_max{{{sel}}})', "max {{instance}}")],
        w=8, desc="Used approaching max means requests queue for a connection — pool exhaustion.")
    b.ts("EF Core failures", [
        (f'sum(increase(microsoft_entityframeworkcore_microsoft_entityframeworkcore_execution_strategy_operation_failures{{{sel}}}[5m]))', "execution-strategy retries"),
        (f'sum(increase(microsoft_entityframeworkcore_microsoft_entityframeworkcore_optimistic_concurrency_failures{{{sel}}}[5m]))', "optimistic concurrency")],
        w=12, bars=True, desc="Retries point at transient DB faults; concurrency failures at competing writers.")
    b.ts("Connection open time (p95)", [
        (f'histogram_quantile(0.95, sum by (le) (rate(npgsql_db_client_connection_npgsql_create_time_bucket{{{sel}}}[5m])))', "p95")],
        unit="s", w=12)

    b.row("Runtime")
    b.ts("CPU (cores)", [(f'rate(process_cpu_seconds_total{{{sel}}}[5m])', "{{instance}}")], unit="none", w=8)
    b.ts("Memory", [
        (f'process_working_set_bytes{{{sel}}}', "working set {{instance}}"),
        (f'dotnet_total_memory_bytes{{{sel}}}', "managed heap {{instance}}")], unit="bytes", w=8)
    b.ts("GC collections / sec", [(f'sum by (generation) (rate(dotnet_collection_count_total{{{sel}}}[5m]))', "gen {{generation}}")],
         unit="ops", w=8)
    b.ts("Thread pool", [
        (f'system_runtime_threadpool_thread_count{{{sel}}}', "threads {{instance}}"),
        (f'system_runtime_threadpool_queue_length{{{sel}}}', "queued work {{instance}}")],
        w=8, desc="A growing queue with a climbing thread count is thread-pool starvation (sync-over-async).")
    b.ts("Exceptions & lock contention / sec", [
        (f'sum(rate(system_runtime_exception_count_total{{{sel}}}[5m]))', "exceptions"),
        (f'sum(rate(system_runtime_monitor_lock_contention_count_total{{{sel}}}[5m]))', "lock contentions")],
        unit="ops", w=8)
    b.ts("Allocation rate", [(f'rate(system_runtime_alloc_total{{{sel}}}[5m])', "{{instance}}")], unit="Bps", w=8)


# ---------------------------------------------------------------- overview
b = Board()
b.row("Service health")
b.stat("API", [(f'min(up{{{API}}}) or vector(0)', "")], mappings=UPMAP,
       steps=[{"color": "red", "value": None}, {"color": "green", "value": 1}], w=3)
b.stat("Worker", [(f'min(up{{{WRK}}}) or vector(0)', "")], mappings=UPMAP,
       steps=[{"color": "red", "value": None}, {"color": "green", "value": 1}], w=3)
b.stat("Firing alerts", [('count(ALERTS{alertstate="firing"}) or vector(0)', "")], steps=RED, w=3)
b.stat("API availability (1h)", [(
    f'1 - (sum(rate(http_request_duration_seconds_count{{{API},code=~"5.."}}[1h])) or vector(0)) / sum(rate(http_request_duration_seconds_count{{{API}}}[1h]))', "")],
    unit="percentunit", decimals=2, w=3, desc="Share of API requests in the last hour that were not 5xx.",
    steps=[{"color": "red", "value": None}, {"color": "orange", "value": 0.99}, {"color": "green", "value": 0.999}])
b.stat("API p99 latency", [(f'histogram_quantile(0.99, sum by (le) (rate(http_request_duration_seconds_bucket{{{API}}}[5m])))', "")],
       unit="s", w=3, steps=[{"color": "green", "value": None}, {"color": "orange", "value": 0.5}, {"color": "red", "value": 1}])
b.stat("Inbox pending", [(f'sum(messaging_backlog_messages{{{WRK},queue="inbox",status="pending"}}) or vector(0)', "")], w=3,
       steps=[{"color": "green", "value": None}, {"color": "orange", "value": 100}, {"color": "red", "value": 500}])
b.stat("Dead-lettered (needs operator)", [(f'sum(messaging_backlog_messages{{{WRK},status="dead_lettered"}}) or vector(0)', "")],
       w=3, steps=RED, desc="Inbox + outbox rows parked in dead_lettered.")
b.stat("Ingestion lag p95", [(f'histogram_quantile(0.95, sum by (le) (rate(messaging_end_to_end_lag_seconds_bucket{{{WRK},queue="inbox"}}[10m])))', "")],
       unit="s", w=3, desc="Receipt of a bank delivery to stored transactions.",
       steps=[{"color": "green", "value": None}, {"color": "orange", "value": 60}, {"color": "red", "value": 300}])
b.table("Firing alerts", 'ALERTS{alertstate="firing"}', h=6,
        desc="Alert rules: monitoring/prometheus-rules.yml (k8s: k8s/monitoring/prometheus/configmap.yaml). No Alertmanager — nothing is paged.")
b.row("Golden signals")
b.ts("API traffic by status class", [
    (f'sum by (class) (label_replace(rate(http_request_duration_seconds_count{{{API}}}[5m]), "class", "${{1}}xx", "code", "([0-9]).."))', "{{class}}")],
    unit="reqps", stack=True)
b.ts("API latency", [
    (f'histogram_quantile(0.50, sum by (le) (rate(http_request_duration_seconds_bucket{{{API}}}[5m])))', "p50"),
    (f'histogram_quantile(0.95, sum by (le) (rate(http_request_duration_seconds_bucket{{{API}}}[5m])))', "p95"),
    (f'histogram_quantile(0.99, sum by (le) (rate(http_request_duration_seconds_bucket{{{API}}}[5m])))', "p99")],
    unit="s", steps=[{"color": "green", "value": None}, {"color": "red", "value": 1}])
b.ts("Deliveries processed / min", [(f'sum by (outcome) (rate(messaging_processing_duration_seconds_count{{{WRK},queue="inbox"}}[5m])) * 60', "{{outcome}}")],
     unit="none", stack=True, desc="Inbound bank deliveries the worker finished, by outcome.")
b.ts("Message backlog", [(f'sum by (queue, status) (messaging_backlog_messages{{{WRK}}})', "{{queue}} {{status}}")])
b.row("Saturation")
b.ts("CPU (cores)", [('sum by (job) (rate(process_cpu_seconds_total{job=~"transaction-.*"}[5m]))', "{{job}}")], unit="none", w=8)
b.ts("Working set", [('sum by (job) (process_working_set_bytes{job=~"transaction-.*"})', "{{job}}")], unit="bytes", w=8,
     steps=[{"color": "green", "value": None}, {"color": "red", "value": 450 * 1024 * 1024}],
     desc="Dashed line: the API's memory alert threshold (450Mi of a 512Mi limit).")
b.ts("DB pool in use", [('sum by (job) (npgsql_db_client_connection_count{job=~"transaction-.*",db_client_connection_state="used"})', "{{job}}")], w=8)
overview = b.dashboard("transaction-overview-v1", "Transaction Aggregation — Overview",
                       "Platform health at a glance: service up/down, firing alerts, API golden signals, ingestion health and saturation.",
                       ["transaction-aggregation", "overview"], [DS_VAR], LINKS)

# ---------------------------------------------------------------- API
SEL = f'{API},instance=~"$instance"'
b = Board()
b.stat("Request rate", [(f'sum(rate(http_request_duration_seconds_count{{{SEL}}}[5m]))', "")], unit="reqps", w=6)
b.stat("Error rate (5xx)", [(f'100 * (sum(rate(http_request_duration_seconds_count{{{SEL},code=~"5.."}}[5m])) or vector(0)) / sum(rate(http_request_duration_seconds_count{{{SEL}}}[5m]))', "")],
       unit="percent", decimals=2, w=6, steps=[{"color": "green", "value": None}, {"color": "orange", "value": 1}, {"color": "red", "value": 5}])
b.stat("P99 latency", [(f'histogram_quantile(0.99, sum by (le) (rate(http_request_duration_seconds_bucket{{{SEL}}}[5m])))', "")],
       unit="s", w=6, steps=[{"color": "green", "value": None}, {"color": "orange", "value": 0.5}, {"color": "red", "value": 1}])
b.stat("In-flight requests", [(f'sum(http_requests_in_progress{{{SEL}}})', "")], w=6)
b.row("HTTP server")
b.ts("Requests by status code", [(f'sum by (code) (rate(http_request_duration_seconds_count{{{SEL}}}[5m]))', "{{code}}")], unit="reqps", stack=True)
b.ts("Latency percentiles", [
    (f'histogram_quantile(0.50, sum by (le) (rate(http_request_duration_seconds_bucket{{{SEL}}}[5m])))', "p50"),
    (f'histogram_quantile(0.95, sum by (le) (rate(http_request_duration_seconds_bucket{{{SEL}}}[5m])))', "p95"),
    (f'histogram_quantile(0.99, sum by (le) (rate(http_request_duration_seconds_bucket{{{SEL}}}[5m])))', "p99")],
    unit="s", steps=[{"color": "green", "value": None}, {"color": "red", "value": 1}])
b.ts("Requests by endpoint", [(f'sum by (method, endpoint) (rate(http_request_duration_seconds_count{{{SEL}}}[5m]))', "{{method}} {{endpoint}}")], unit="reqps")
b.ts("p95 latency by endpoint", [(f'histogram_quantile(0.95, sum by (le, method, endpoint) (rate(http_request_duration_seconds_bucket{{{SEL}}}[5m])))', "{{method}} {{endpoint}}")], unit="s")
b.ts("4xx by endpoint", [(f'sum by (code, method, endpoint) (rate(http_request_duration_seconds_count{{{SEL},code=~"4.."}}[5m]))', "{{code}} {{method}} {{endpoint}}")],
     unit="reqps", desc="401/403 spikes: auth or ownership problems; 429: rate limiting; 400: client contract drift.")
b.ts("5xx by endpoint", [(f'sum by (code, method, endpoint) (rate(http_request_duration_seconds_count{{{SEL},code=~"5.."}}[5m]))', "{{code}} {{method}} {{endpoint}}")], unit="reqps")
b.ts("Kestrel connections", [
    (f'sum by (instance) (microsoft_aspnetcore_server_kestrel_kestrel_active_connections{{{SEL}}})', "active {{instance}}"),
    (f'sum by (instance) (microsoft_aspnetcore_server_kestrel_kestrel_queued_connections{{{SEL}}})', "queued {{instance}}")], w=12)
b.ts("Webhook duplicates dropped / min", [('sum by (source_name, level) (rate(inbound_duplicates_total[5m])) * 60', "{{source_name}} ({{level}})")],
     w=12, desc="Redelivered bank webhooks recognised by idempotency key (message) or by transaction id (transaction).")
b.row("Outbound dependencies (HttpClient: Keycloak, bank aggregator)")
b.ts("Outbound requests by host / status", [(f'sum by (server_address, http_response_status_code) (rate(system_net_http_http_client_request_duration_count{{{SEL}}}[5m]))', "{{server_address}} {{http_response_status_code}}")], unit="reqps")
b.ts("Outbound p95 latency by host", [(f'histogram_quantile(0.95, sum by (le, server_address) (rate(system_net_http_http_client_request_duration_bucket{{{SEL}}}[5m])))', "{{server_address}}")], unit="s")
runtime_and_db(b, SEL)
api = b.dashboard("transaction-api-v1", "Transaction Aggregation API",
                  "Transaction API: HTTP (prometheus-net), outbound HTTP, PostgreSQL and .NET runtime.",
                  ["transaction-aggregation", "api"], [DS_VAR, instance_var(API)], LINKS)

# ---------------------------------------------------------------- pipeline
SEL = f'{WRK},instance=~"$instance"'
b = Board()
b.stat("Inbox pending", [(f'sum(messaging_backlog_messages{{{SEL},queue="inbox",status="pending"}}) or vector(0)', "")], w=4,
       steps=[{"color": "green", "value": None}, {"color": "orange", "value": 100}, {"color": "red", "value": 500}])
b.stat("Outbox pending", [(f'sum(messaging_backlog_messages{{{SEL},queue="outbox",status="pending"}}) or vector(0)', "")], w=4,
       steps=[{"color": "green", "value": None}, {"color": "orange", "value": 100}, {"color": "red", "value": 500}])
b.stat("Inbox dead-lettered", [(f'sum(messaging_backlog_messages{{{SEL},queue="inbox",status="dead_lettered"}}) or vector(0)', "")], w=4, steps=RED)
b.stat("Outbox dead-lettered", [(f'sum(messaging_backlog_messages{{{SEL},queue="outbox",status="dead_lettered"}}) or vector(0)', "")], w=4, steps=RED)
b.stat("Ingestion lag p95", [(f'histogram_quantile(0.95, sum by (le) (rate(messaging_end_to_end_lag_seconds_bucket{{{SEL},queue="inbox"}}[10m])))', "")],
       unit="s", w=4, steps=[{"color": "green", "value": None}, {"color": "orange", "value": 60}, {"color": "red", "value": 300}])
b.stat("Kafka consumer crashes (24h)", [(f'sum(increase(kafka_bank_transactions_consumer_crashes_total{{{SEL}}}[24h])) or vector(0)', "")], w=4, steps=RED)
b.row("Inbox / outbox")
b.ts("Backlog", [(f'sum by (queue, status) (messaging_backlog_messages{{{SEL}}})', "{{queue}} {{status}}")],
     desc="pending = waiting to be processed; dead_lettered = exhausted retries, needs an operator.")
b.ts("Messages processed / sec", [(f'sum by (queue, outcome) (rate(messaging_processing_duration_seconds_count{{{SEL}}}[5m]))', "{{queue}} {{outcome}}")], unit="ops")
b.ts("Processing time per message", [
    (f'histogram_quantile(0.50, sum by (le, queue) (rate(messaging_processing_duration_seconds_bucket{{{SEL}}}[5m])))', "p50 {{queue}}"),
    (f'histogram_quantile(0.95, sum by (le, queue) (rate(messaging_processing_duration_seconds_bucket{{{SEL}}}[5m])))', "p95 {{queue}}")], unit="s")
b.ts("End-to-end lag", [
    (f'histogram_quantile(0.50, sum by (le, queue) (rate(messaging_end_to_end_lag_seconds_bucket{{{SEL}}}[10m])))', "p50 {{queue}}"),
    (f'histogram_quantile(0.95, sum by (le, queue) (rate(messaging_end_to_end_lag_seconds_bucket{{{SEL}}}[10m])))', "p95 {{queue}}")],
    unit="s", steps=[{"color": "green", "value": None}, {"color": "red", "value": 300}],
    desc="Inbox: received → transactions stored. Outbox: event occurred → published. Dashed: IngestionLagHigh threshold.")
b.ts("Dead-lettered / 15m", [
    (f'sum by (source_name) (increase(inbox_messages_dead_lettered_total{{{SEL}}}[15m]))', "inbox {{source_name}}"),
    (f'sum by (message_type) (increase(outbox_messages_dead_lettered_total{{{SEL}}}[15m]))', "outbox {{message_type}}")],
    bars=True, desc="Any bar here fires Inbox/OutboxMessagesDeadLettering. LastError on the row has the reason.")
b.ts("Duplicates dropped / min", [('sum by (source_name, level) (rate(inbound_duplicates_total[5m])) * 60', "{{source_name}} ({{level}})")],
     desc="Across API (webhook deliveries) and worker (individual transactions).")
b.ts("Archived to messaging.*Archive / h", [(f'sum by (queue) (increase(messaging_archived_messages_total{{{SEL}}}[1h]))', "{{queue}}")],
     w=12, bars=True, desc="Processed rows older than MessageArchive:RetainDays moved out of the hot queue tables. Nothing is deleted.")
b.ts("Worker replicas", [(f'count(up{{{WRK}}} == 1)', "running")], w=12,
     desc="Scaled by KEDA on the pending backlog (k8s/worker/scaledobject.yaml), or by the CPU HPA fallback.")
b.row("Kafka (bank-transactions topic)")
b.ts("Records consumed by outcome / sec", [(f'sum by (outcome) (rate(kafka_bank_transactions_messages_total{{{SEL}}}[5m]))', "{{outcome}}")], unit="ops", w=12, stack=True)
b.ts("Failures", [
    (f'sum(increase(kafka_bank_transactions_transient_failures_total{{{SEL}}}[5m])) or vector(0)', "transient (retried)"),
    (f'sum(increase(kafka_bank_transactions_permanent_failures_total{{{SEL}}}[5m])) or vector(0)', "permanent (→ DLQ)"),
    (f'sum(increase(kafka_bank_transactions_consumer_crashes_total{{{SEL}}}[5m])) or vector(0)', "consumer crashes")], w=12, bars=True)
runtime_and_db(b, SEL)
pipeline = b.dashboard("transaction-pipeline-v1", "Transaction Ingestion Pipeline",
                       "Worker: inbox/outbox dispatch, integration-event publishing, Kafka consumer, PostgreSQL and .NET runtime.",
                       ["transaction-aggregation", "worker"], [DS_VAR, instance_var(WRK)], LINKS)

boards = {"overview.json": overview, "transaction-api.json": api, "ingestion-pipeline.json": pipeline}
ddir = os.path.join(ROOT, "monitoring", "grafana", "dashboards")
for name, d in boards.items():
    with open(os.path.join(ddir, name), "w", encoding="utf-8", newline="\n") as f:
        json.dump(d, f, indent=2, ensure_ascii=False); f.write("\n")

cm = ["# Generated by monitoring/grafana/generate_dashboards.py — edit that script and rerun it",
      "# rather than editing here, so docker-compose, Aspire and k8s show the same dashboards.",
      "apiVersion: v1", "kind: ConfigMap", "metadata:", "  name: grafana-dashboards",
      "  namespace: transaction-aggregation", "  labels:", "    app.kubernetes.io/name: grafana",
      "    app.kubernetes.io/component: monitoring", "    app.kubernetes.io/part-of: transaction-aggregation", "data:"]
for name, d in boards.items():
    cm.append(f"  {name}: |")
    cm += ["    " + l for l in json.dumps(d, indent=2, ensure_ascii=False).splitlines()]
with open(os.path.join(ROOT, "k8s", "monitoring", "grafana", "configmap-dashboards.yaml"), "w", encoding="utf-8", newline="\n") as f:
    f.write("\n".join(cm) + "\n")
print("ok", {k: len(v["panels"]) for k, v in boards.items()})
