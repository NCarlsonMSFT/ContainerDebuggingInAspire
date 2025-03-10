var builder = DistributedApplication.CreateBuilder(args);

#if RUN_IN_CONTAINER

ContainerHelpers.DownloadVsdbg(Path.Combine(builder.AppHostDirectory, "vsdbg"));

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
                    .WithReference(apiService.GetEndpoint("http"));

#else

var apiService = builder.AddProject<Projects.ContainerDebugging_ApiService>("apiservice");

var webfrontend = builder.AddProject<Projects.ContainerDebugging_Web>("webfrontend")
                         .WithReference(apiService);

#endif

webfrontend.WithExternalHttpEndpoints()
           .WaitFor(apiService);

builder.Build().Run();
