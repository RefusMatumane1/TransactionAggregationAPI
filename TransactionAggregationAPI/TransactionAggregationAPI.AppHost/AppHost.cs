using Microsoft.Extensions.Configuration;

var builder = DistributedApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("secrets.json", optional: true, reloadOnChange: false);

var pgPassword = builder.AddParameter("postgres-password", secret: true);
var keycloakAdminPassword = builder.AddParameter("keycloak-admin-password", secret: true);
var grafanaAdminPassword = builder.AddParameter("grafana-admin-password", secret: true);
var appAdminPassword = builder.AddParameter("app-admin-password", secret: true);
var appStaffPassword = builder.AddParameter("app-staff-password", secret: true);

var postgres = builder
    .AddPostgres("transaction-db", password: pgPassword)
    .WithImageTag("17.6")
    .WithPgAdmin()
    .WithDataVolume("transaction-postgres-data")
    .WithLifetime(ContainerLifetime.Persistent);

var transactionDb = postgres.AddDatabase("transactiondb");

var redis = builder.AddRedis("redis")
    .WithDataVolume("transaction-redis-data")
     .WithRedisInsight()
     .WithLifetime(ContainerLifetime.Persistent);

var seq = builder.AddSeq("seq")
    .WithDataVolume("transaction-seq-data")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithEnvironment("ACCEPT_EULA", "Y");

var kafka = builder.AddKafka("kafka")
    .WithDataVolume("transaction-kafka-data")
    .WithKafkaUI(ui => ui.WithHostPort(8083))
    .WithLifetime(ContainerLifetime.Persistent);

var keycloak = builder
    .AddContainer("keycloak", "quay.io/keycloak/keycloak")
    .WithImageTag("26.0")
    .WithBindMount(
        "../keycloak/realm-export.json",
        "/opt/keycloak/data/import/realm-export.json",
        isReadOnly: true)
        .WithVolume("transaction-keycloak-data", "/opt/keycloak/data")
    .WithArgs("start-dev", "--import-realm")
    .WithEnvironment("KEYCLOAK_ADMIN", "admin")
    .WithEnvironment("KEYCLOAK_ADMIN_PASSWORD", keycloakAdminPassword)
    .WithEnvironment("TRANSACTION_APP_ADMIN_PASSWORD", appAdminPassword)
    .WithEnvironment("TRANSACTION_APP_STAFF_PASSWORD", appStaffPassword)
    .WithEnvironment("KC_HOSTNAME", "http://localhost:8081")
    .WithEnvironment("KC_HOSTNAME_STRICT", "false")
    .WithEnvironment("KC_HTTP_ENABLED", "true")
    .WithHttpEndpoint(
        port: 8081,
        targetPort: 8080,
        name: "http")
    .WithLifetime(ContainerLifetime.Persistent);

var api = builder
    .AddProject<Projects.TransactionAggregationAPI>("transactionaggregationapi")
    .WithReference(seq)
    .WaitForStart(seq);

api.WithReference(transactionDb)
    .WaitForStart(transactionDb)
    .WithReference(redis)
    .WaitForStart(redis)
    .WaitFor(keycloak);

builder
    .AddProject<Projects.TransactionAggregation_Worker>("transactionaggregationworker")
    .WithReference(seq)
    .WaitForStart(seq)
    .WithReference(transactionDb)
    .WaitForStart(transactionDb)
    .WithReference(redis)
    .WaitForStart(redis)
    .WithReference(kafka)
    .WaitFor(kafka)
    .WaitForStart(api);

var mockAggregatorApiKey = builder.AddParameter("mock-aggregator-api-key", secret: true);

var mockAggregator = builder.AddProject<Projects.TransactionAggregation_MockAggregator>("mockaggregator");

var apiHttps = api.GetEndpoint("https");
api.WithEnvironment("MockAggregator__WebhookApiKey", mockAggregatorApiKey);

mockAggregator
    .WithReference(seq)
    .WithReference(kafka)
    .WithEnvironment("Feed__ApiBaseUrl", apiHttps)
    .WithEnvironment("Feed__ApiKey", mockAggregatorApiKey)
    .WaitForStart(api);

var prometheus = builder
    .AddContainer("prometheus", "prom/prometheus")
    .WithImageTag("v2.55.1")
    .WithBindMount("monitoring/prometheus-aspire.yml", "/etc/prometheus/prometheus.yml", isReadOnly: true)
    .WithBindMount("../monitoring/prometheus-rules.yml", "/etc/prometheus/rules.yml", isReadOnly: true)
    .WithContainerRuntimeArgs("--add-host=host.docker.internal:host-gateway")
    .WithArgs("--config.file=/etc/prometheus/prometheus.yml", "--storage.tsdb.path=/prometheus", "--web.enable-lifecycle")
    .WithHttpEndpoint(port: 9090, targetPort: 9090, name: "web");

builder
    .AddContainer("grafana", "grafana/grafana")
    .WithImageTag("11.3.0")
    .WithBindMount("../monitoring/grafana/provisioning", "/etc/grafana/provisioning", isReadOnly: true)
    .WithBindMount("../monitoring/grafana/dashboards", "/var/lib/grafana/dashboards", isReadOnly: true)
    .WithEnvironment("GF_SECURITY_ADMIN_USER", "admin")
    .WithEnvironment("GF_SECURITY_ADMIN_PASSWORD", grafanaAdminPassword)
    .WithEnvironment("GF_USERS_ALLOW_SIGN_UP", "false")
    .WithHttpEndpoint(port: 3000, targetPort: 3000, name: "web")
    .WaitFor(prometheus);

builder.Build().Run();