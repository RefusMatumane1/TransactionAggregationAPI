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

// Second inbound channel for bank transactions (alongside the REST webhook). Kafka UI is
// for inspecting/producing test records on the bank-transactions topic and its .dlq.
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
    .WithArgs("start-dev", "--import-realm")
    .WithEnvironment("KEYCLOAK_ADMIN", "admin")
    .WithEnvironment("KEYCLOAK_ADMIN_PASSWORD", "admin")
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

// Kafka consumer + inbox/outbox/pending-expiry dispatchers. Started after the API because
// the API applies migrations in Development; the dispatchers retry until the tables exist.
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

// Development stand-in for the external account aggregator: the consent flow BankLinks
// needs to link an account, and mock FNB/Absa/Capitec/Standard Bank feeds pushed through
// the real webhook (or Kafka, with Feed__Channel=Kafka). The two apps share a client secret
// and the webhook source's API key; the API registers that source at startup in Development.
var mockAggregatorApiKey = builder.AddParameter("mock-aggregator-api-key", secret: true);
var mockAggregatorClientSecret = builder.AddParameter("mock-aggregator-client-secret", secret: true);

var mockAggregator = builder.AddProject<Projects.TransactionAggregation_MockAggregator>("mockaggregator");

var apiHttp = api.GetEndpoint("http");
var mockAggregatorHttp = mockAggregator.GetEndpoint("http");
// The consent page returns the browser to the UI (which the API hosts here), and the UI
// completes the link through the API's callback endpoint. It must be the https origin: that
// is where the UI signs in (Keycloak only accepts https://localhost:5101), so it is where
// the user's session lives when they come back.
var bankLinkCallback = ReferenceExpression.Create($"{api.GetEndpoint("https")}/bank-links/callback");

api.WithEnvironment("BankAggregator__ClientId", "transaction-aggregation-dev")
    .WithEnvironment("BankAggregator__ClientSecret", mockAggregatorClientSecret)
    .WithEnvironment("BankAggregator__AuthorizeEndpoint", ReferenceExpression.Create($"{mockAggregatorHttp}/oauth/authorize"))
    .WithEnvironment("BankAggregator__TokenEndpoint", ReferenceExpression.Create($"{mockAggregatorHttp}/oauth/token"))
    .WithEnvironment("BankAggregator__AccountEndpoint", ReferenceExpression.Create($"{mockAggregatorHttp}/accounts/me"))
    .WithEnvironment("BankAggregator__RedirectUri", bankLinkCallback)
    .WithEnvironment("MockAggregator__WebhookApiKey", mockAggregatorApiKey);

mockAggregator
    .WithReference(seq)
    .WithReference(kafka)
    .WithEnvironment("MockAggregator__ClientSecret", mockAggregatorClientSecret)
    .WithEnvironment("MockAggregator__AllowedRedirectUris__0", bankLinkCallback)
    .WithEnvironment("Feed__ApiBaseUrl", apiHttp)
    .WithEnvironment("Feed__ApiKey", mockAggregatorApiKey)
    .WaitForStart(api);

var prometheus = builder
    .AddContainer("prometheus", "prom/prometheus")
    .WithBindMount("monitoring/prometheus-aspire.yml", "/etc/prometheus/prometheus.yml", isReadOnly: true)
    .WithEnvironment("API_METRICS_TARGET",
        ReferenceExpression.Create($"host.docker.internal:5100"))
    .WithEnvironment("WORKER_METRICS_TARGET",
        ReferenceExpression.Create($"host.docker.internal:5110"))
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