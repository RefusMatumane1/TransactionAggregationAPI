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

var api = builder
    .AddProject<Projects.TransactionAggregationAPI>("transactionaggregationapi")
    .WithHttpEndpoint(port: 5001, name: "public")
    .WithReference(seq)
    .WaitForStart(seq);

api.WithReference(transactionDb)
    .WaitForStart(transactionDb)
    .WithReference(redis)
    .WaitForStart(redis);

builder.Build().Run();
