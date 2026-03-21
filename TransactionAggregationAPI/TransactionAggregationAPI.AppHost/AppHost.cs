var builder = DistributedApplication.CreateBuilder(args);

var posgress = builder.AddPostgres("transaction-db")
    .WithDataVolume()
    .WithPgAdmin()
    .WithLifetime(ContainerLifetime.Persistent);

var redis = builder.AddRedis("transaction-cache")
    .WithDataVolume("transaction-redis-data")
    .WithLifetime(ContainerLifetime.Persistent);

var seq = builder.AddSeq("seq")
    .WithDataVolume("transaction-seq-data")
    .WithLifetime(ContainerLifetime.Persistent);

builder.AddProject<Projects.TransactionAggregationAPI>("transactionaggregationapi")
    .WithHttpsEndpoint(5001, name:"public")
    .WithReference(posgress)
    .WaitForStart(posgress)
    .WithReference(redis)
    .WaitForStart(redis)
    .WithReference(seq);

builder.Build().Run();
