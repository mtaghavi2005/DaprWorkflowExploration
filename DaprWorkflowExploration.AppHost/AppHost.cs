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
                AppPort = 5499,
                DaprHttpPort = 58199,
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
                AppPort = 5497,
                DaprHttpPort = 58197,
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
        AppPort = 5498,
        DaprHttpPort = 58198,
    })
    .WithReference(apiService)
    .WaitFor(apiService)
    .WaitFor(paymentProcessingService);

builder.Build().Run();
