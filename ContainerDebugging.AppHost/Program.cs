var builder = DistributedApplication.CreateBuilder(args);

#if RUN_IN_CONTAINER

// Ensure vsdbg has been downloaded
ContainerHelpers.DownloadVsdbg(Path.Combine(builder.AppHostDirectory, "vsdbg"));

// Configure the Apire host to use a cert that handles host.docker.internal
(string certPath, string certPassword) = ContainerHelpers.EnsureGeneratedCertificate(builder);
Environment.SetEnvironmentVariable("ASPNETCORE_Kestrel__Certificates__Default__Path", certPath);
Environment.SetEnvironmentVariable("ASPNETCORE_Kestrel__Certificates__Default__Password", certPassword);

var apiService = builder.AddDebuggableContainer(
                            name: "apiservice",
                            projectName: "ContainerDebugging.ApiService",
                            image: "containerdebuggingapiservice",
                            tag: "dev");
    
var webfrontend = builder.AddDebuggableContainer(
                            name: "webfrontend",
                            projectName: "ContainerDebugging.Web",
                            image: "containerdebuggingweb",
                            tag: "dev")
                    .WithReference(apiService.GetEndpoint("http"))
                    .WithReference(apiService.GetEndpoint("https"));

#else

var apiService = builder.AddProject<Projects.ContainerDebugging_ApiService>("apiservice");

var webfrontend = builder.AddProject<Projects.ContainerDebugging_Web>("webfrontend")
                         .WithReference(apiService);

#endif

webfrontend.WithExternalHttpEndpoints()
           .WaitFor(apiService);

builder.Build().Run();
