using CommunityToolkit.Aspire.Hosting.Dapr;

var builder = DistributedApplication.CreateBuilder(args);
var localDaprComponentsPath = Path.Combine(builder.AppHostDirectory, "dapr", "components", "local");

var stateStore = builder.AddDaprStateStore("statestore", new DaprComponentOptions
{
    LocalPath = Path.Combine(localDaprComponentsPath, "statestore.yaml")
});

var paymentProcessingService = builder.AddProject<Projects.DaprWorkflowExploration_AccountingService>("accountingservice")
    .WithHttpHealthCheck("/health")
    .WithDaprSidecar(sidecar =>
    {
        sidecar
            .WithOptions(new DaprSidecarOptions
            {
                AppId = "accountingservice",
                ResourcesPaths = [localDaprComponentsPath]
            });
    });

var apiService = builder.AddProject<Projects.DaprWorkflowExploration_ApiService>("apiservice")
    .WithHttpHealthCheck("/health")
    .WithDaprSidecar(sidecar =>
    {
        sidecar
            .WithOptions(new DaprSidecarOptions
            {
                AppId = "apiservice",
                ResourcesPaths = [localDaprComponentsPath]
            })
            .WithReference(stateStore);
    })
    .WaitFor(paymentProcessingService);

builder.AddProject<Projects.DaprWorkflowExploration_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithDaprSidecar(new DaprSidecarOptions()
    {
        AppId = "webfrontend",
    })
    .WithReference(apiService)
    .WaitFor(apiService)
    .WaitFor(paymentProcessingService);

builder.Build().Run();
