var builder = DistributedApplication.CreateBuilder(args);

var apiService = builder.AddProject<Projects.ContainerDebugging_ApiService>("apiservice");

builder.AddProject<Projects.ContainerDebugging_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithReference(apiService)
    .WaitFor(apiService);

builder.Build().Run();
