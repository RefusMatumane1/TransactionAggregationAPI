var builder = DistributedApplication.CreateBuilder(args);

var posgress = builder.AddPostgres("transaction-db")
    .WithDataVolume()
    .WithPgAdmin();

var redis = builder.AddRedis("transaction-cache").WithDataVolume();

builder.AddProject<Projects.TransactionAggregationAPI>("transactionaggregationapi")
    .WithHttpsEndpoint(5001, name:"public")
    .WithReference(posgress)
    .WaitForStart(posgress)
    .WithReference(redis)
    .WaitForStart(redis);

builder.Build().Run();
