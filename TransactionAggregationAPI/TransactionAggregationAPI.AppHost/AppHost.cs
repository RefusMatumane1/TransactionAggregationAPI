var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.TransactionAggregationAPI>("transactionaggregationapi");

builder.Build().Run();
