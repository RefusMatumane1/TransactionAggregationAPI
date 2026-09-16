var builder = DistributedApplication.CreateBuilder(args);

var pgPassword = builder.AddParameter("postgres-password", secret: true);

var postgres = builder
    .AddPostgres("transaction-db", password: pgPassword)
    .WithPgAdmin()
    .WithDataVolume("transaction-postgres-data")
    .WithLifetime(ContainerLifetime.Persistent);

var transactionDb = postgres.AddDatabase("transactiondb");

var redis = builder.AddRedis("redis")
    .WithDataVolume("transaction-redis-data")
     .WithRedisInsight();

var seq = builder.AddSeq("seq")
    .WithDataVolume("transaction-seq-data")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithEnvironment("ACCEPT_EULA", "Y");

var keycloak = builder
    .AddContainer("keycloak", "quay.io/keycloak/keycloak")
    .WithImageTag("26.0")
    .WithBindMount("../keycloak/realm-export.json", "/opt/keycloak/data/import/realm-export.json", isReadOnly: true)
    .WithArgs("start-dev", "--import-realm")
    .WithEnvironment("KEYCLOAK_ADMIN", "admin")
    .WithEnvironment("KEYCLOAK_ADMIN_PASSWORD", "admin")
    .WithEnvironment("KC_HOSTNAME", "localhost:8081")
    .WithEnvironment("KC_HOSTNAME_STRICT_HTTPS", "false")
    .WithEnvironment("KC_HTTP_ENABLED", "true")
    .WithHttpEndpoint(port: 8081, targetPort: 8080, name: "http")
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

var prometheus = builder
    .AddContainer("prometheus", "prom/prometheus")
    .WithBindMount("monitoring/prometheus-aspire.yml", "/etc/prometheus/prometheus.yml", isReadOnly: true)
    .WithEnvironment("API_METRICS_TARGET",
        ReferenceExpression.Create($"host.docker.internal:5100"))
    .WithHttpEndpoint(targetPort: 9090, name: "web");

builder
    .AddContainer("grafana", "grafana/grafana")
    .WithBindMount("../monitoring/grafana/provisioning", "/etc/grafana/provisioning", isReadOnly: true)
    .WithBindMount("../monitoring/grafana/dashboards", "/var/lib/grafana/dashboards", isReadOnly: true)
    .WithEnvironment("GF_AUTH_ANONYMOUS_ENABLED", "true")
    .WithEnvironment("GF_AUTH_ANONYMOUS_ORG_ROLE", "Admin")
    .WithHttpEndpoint(targetPort: 3000, name: "web")
    .WaitFor(prometheus);

builder.Build().Run();