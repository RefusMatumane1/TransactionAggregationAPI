using Aspire.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("transaction-db")
    .WithPgAdmin()
    .WithLifetime(ContainerLifetime.Persistent);

var transactionDb = postgres.AddDatabase("transactiondb");

var redis = builder.AddRedis("redis")
    .WithDataVolume("transaction-redis-data")
    .WithLifetime(ContainerLifetime.Persistent);

var seq = builder.AddSeq("seq")
    .WithDataVolume("transaction-seq-data")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithEnvironment("ACCEPT_EULA", "Y"); ;

var api = builder.AddProject<Projects.TransactionAggregationAPI>("transactionaggregationapi")
    .WithHttpsEndpoint(5001, name: "public")
     .WithReference(seq); ;

//var prometheus = builder.AddContainer("prometheus", "prom/prometheus")
//    .WithBindMount("./prometheus", "/etc/prometheus")
//    .WithHttpEndpoint(port: 9090, targetPort: 9090)
//    .WithLifetime(ContainerLifetime.Persistent);


api.WithReference(transactionDb)
    .WaitForStart(transactionDb)
    .WithReference(redis)
    .WaitForStart(redis)
    .WithReference(seq)
    .WaitForStart(seq);

builder.Build().Run();
