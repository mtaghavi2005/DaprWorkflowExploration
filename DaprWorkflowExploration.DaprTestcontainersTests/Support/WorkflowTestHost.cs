using Dapr.Testcontainers.Common.Options;
using Dapr.Testcontainers.Containers;
using Dapr.Testcontainers.Harnesses;

namespace DaprWorkflowExploration.DaprTestcontainersTests.Support;

internal sealed class WorkflowTestHost : IAsyncDisposable
{
    private readonly TestComponentsDirectory componentsDirectory;
    private readonly DotNetProjectAppProcess appProcess;
    private readonly WorkflowHarness harness;
    private readonly DaprTestEnvironment environment;

    private WorkflowTestHost(
        TestComponentsDirectory componentsDirectory,
        DotNetProjectAppProcess appProcess,
        WorkflowHarness harness,
        DaprTestEnvironment environment)
    {
        this.componentsDirectory = componentsDirectory;
        this.appProcess = appProcess;
        this.harness = harness;
        this.environment = environment;
    }

    public static async Task<WorkflowTestHost> StartAsync(WorkflowTestHostOptions options, CancellationToken cancellationToken)
    {
        var projectPaths = ProjectPaths.FromMarkerType(
            options.SolutionDirectory,
            options.ProjectMarkerType,
            options.ComponentsDirectoryName);

        var logsDirectory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"dapr-container-logs-{Guid.NewGuid():N}");
        Directory.CreateDirectory(logsDirectory);
        Console.WriteLine($"Dapr container logs directory: {logsDirectory}");

        var daprOptions = new DaprRuntimeOptions(options.DaprVersion)
            .WithAppId(options.AppId)
            .WithContainerLogs(logsDirectory);

        var environment = await DaprTestEnvironment.CreateWithPooledNetworkAsync(
            daprOptions,
            needsActorState: options.NeedsActorState,
            cancellationToken);

        if (environment.RedisContainer is null && options.ComponentPreparation.RequiresRedisHost)
        {
            throw new InvalidOperationException(
                "WorkflowTestHost requires a Redis container in the Dapr test environment. Set NeedsActorState to true or stop using Redis-dependent component mutations.");
        }

        var redisHost = environment.RedisContainer is null
            ? string.Empty
            : $"{environment.RedisContainer.NetworkAlias}:{RedisContainer.ContainerPort}";
        var mutationContext = new ComponentMutationContext(
            RedisHost: environment.RedisContainer is null ? null : redisHost);

        var componentsDirectory = TestComponentsDirectory.CreateFrom(
            projectPaths.ComponentsDirectory,
            mutationContext,
            options.ComponentPreparation);

        DotNetProjectAppProcess? appProcess = null;
        WorkflowHarness? harness = null;

        try
        {
            harness = new WorkflowHarness(
                componentsDirectory.Path,
                startApp: async appPort =>
                {
                    appProcess = DotNetProjectAppProcess.Start(
                        options.SolutionDirectory,
                        projectPaths.ProjectPath,
                        appPort,
                        harness!.DaprHttpPort,
                        harness.DaprGrpcPort,
                        options.Configuration);

                    await appProcess.WaitUntilHealthyAsync(options.HealthPath, options.StartTimeout, cancellationToken);
                },
                daprOptions,
                environment);

            await harness.InitializeAsync(cancellationToken);
            return new WorkflowTestHost(componentsDirectory, appProcess!, harness, environment);
        }
        catch
        {
            if (appProcess is not null)
            {
                await appProcess.DisposeAsync();
            }

            if (harness is not null)
            {
                await harness.DisposeAsync();
            }

            await environment.DisposeAsync();
            componentsDirectory.Dispose();
            throw;
        }
    }

    public HttpClient CreateClient() => appProcess.CreateClient();

    public async ValueTask DisposeAsync()
    {
        await appProcess.DisposeAsync();
        await harness.DisposeAsync();
        await environment.DisposeAsync();
        componentsDirectory.Dispose();
    }
}

internal sealed record WorkflowTestHostOptions
{
    public required string SolutionDirectory { get; init; }
    public required Type ProjectMarkerType { get; init; }
    public required string AppId { get; init; }
    public ComponentPreparationOptions ComponentPreparation { get; init; } = new();
    public string ComponentsDirectoryName { get; init; } = "components";
    public string HealthPath { get; init; } = "/health";
    public string Configuration { get; init; } = "Debug";
    public string DaprVersion { get; init; } = "1.17.0";
    public bool NeedsActorState { get; init; } = true;
    public TimeSpan StartTimeout { get; init; } = TimeSpan.FromMinutes(2);
}
