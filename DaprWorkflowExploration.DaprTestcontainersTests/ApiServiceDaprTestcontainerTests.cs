using System.Net;
using System.Net.Http.Json;
using DaprWorkflowExploration.DaprTestcontainersTests.Support;

namespace DaprWorkflowExploration.DaprTestcontainersTests;

[TestClass]
public sealed class ApiServiceDaprTestcontainerTests
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(2);

    [TestMethod]
    public async Task ApiService_Starts_With_Dapr_Sidecar_And_StateStore()
    {
        using var timeout = new CancellationTokenSource(DefaultTimeout);
        var cancellationToken = timeout.Token;

        var solutionDirectory = GetSolutionDirectory();
        await using var host = await WorkflowTestHost.StartAsync(
            new WorkflowTestHostOptions
            {
                SolutionDirectory = solutionDirectory,
                ProjectMarkerType = typeof(DaprWorkflowExploration.ApiService.StoreInfo),
                AppId = "apiservice",
                StartTimeout = DefaultTimeout,
                ComponentPreparation = new ComponentPreparationOptions
                {
                    RequiresRedisHost = true,
                    ComponentFilesToDrop = ["statestore.yaml", "secretstore.local.env.yaml"],
                    Mutations =
                    [
                        new ComponentMetadataValueMutation(
                            ComponentFile: "pubsub.yaml",
                            ComponentName: "pubsub",
                            MetadataName: "redisHost",
                            ValueFactory: ComponentMutationValueFactories.RedisHost)
                    ]
                }
            },
            cancellationToken);

        using var client = host.CreateClient();
        var store = new TestStoreInfo(
            Id: "testcontainers-store",
            Name: "Testcontainers Store",
            Description: "Seeded by the Dapr.Testcontainers test.",
            Price: 12.50m,
            Quantity: 7);

        using var createResponse = await client.PostAsJsonAsync("/store", store, cancellationToken);

        Assert.AreEqual(HttpStatusCode.Created, createResponse.StatusCode);

        var stored = await client.GetFromJsonAsync<TestStoreInfo>($"/store/{store.Id}", cancellationToken);

        Assert.IsNotNull(stored);
        Assert.AreEqual(store.Id, stored.Id);
        Assert.AreEqual(store.Quantity, stored.Quantity);

        using var metadataResponse = await client.GetAsync("/dapr/metadata", cancellationToken);

        Assert.AreEqual(HttpStatusCode.OK, metadataResponse.StatusCode);
    }

    private static string GetSolutionDirectory()
    {
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));
    }

    private sealed record TestStoreInfo(string Id, string Name, string Description, decimal Price, int Quantity);
}
