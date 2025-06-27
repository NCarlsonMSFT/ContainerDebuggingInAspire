var builder = DistributedApplication.CreateBuilder(args);

#if RUN_IN_CONTAINER
string? apiServiceLaunchProfileName = "Container (.NET SDK)";
string? webFrontEndLaunchProfileName = "Container (.NET SDK)";

// Configure the Aspire host to use a cert that handles host.docker.internal
(string certPath, string certPassword) = ContainerHelpers.EnsureGeneratedCertificate(builder);
Environment.SetEnvironmentVariable("ASPNETCORE_Kestrel__Certificates__Default__Path", certPath);
Environment.SetEnvironmentVariable("ASPNETCORE_Kestrel__Certificates__Default__Password", certPassword);
#else
string? apiServiceLaunchProfileName = null;
string? webFrontEndLaunchProfileName = null;
#endif

var apiService = builder.AddProject<Projects.ContainerDebugging_ApiService>("apiservice", apiServiceLaunchProfileName)
#if RUN_IN_CONTAINER
                    .WithDebuggableContainer(hostHttpPort: 5307, hostHttpsPort: 7492)
#endif
;

var webfrontend = builder.AddProject<Projects.ContainerDebugging_Web>("webfrontend", webFrontEndLaunchProfileName)
#if RUN_IN_CONTAINER
                    .WithDebuggableContainer(hostHttpPort: 5192, hostHttpsPort: 7126)
#endif
                    .WithReference(apiService)
                    .WithExternalHttpEndpoints()
                    .WaitFor(apiService);

builder.Build().Run();
