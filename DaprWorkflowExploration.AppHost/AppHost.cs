using CommunityToolkit.Aspire.Hosting.Dapr;

var builder = DistributedApplication.CreateBuilder(args);

var apiService = builder.AddProject<Projects.DaprWorkflowExploration_ApiService>("apiservice")
    .WithHttpHealthCheck("/health")
    .WithDaprSidecar(new DaprSidecarOptions
    {
        AppId = "apiservice",
        AppPort = 5497
    });

builder.AddProject<Projects.DaprWorkflowExploration_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(apiService)
    .WaitFor(apiService);

builder.Build().Run();
