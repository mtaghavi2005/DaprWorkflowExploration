namespace DaprWorkflowExploration.IntegrationTests.TestUtils;

public sealed class WorkflowScenarioContext
{
    public AspireAppFixture? App { get; set; }
    public Uri? DashboardUrl { get; set; }
    public string? WorkflowInstanceId { get; set; }

    public HttpClient CreateApiClient()
    {
        if (App is null)
        {
            throw new InvalidOperationException("The Aspire AppHost has not been started.");
        }

        return App.CreateApiClient();
    }

    public Uri GetApiDaprGrpcEndpoint()
    {
        if (App is null)
        {
            throw new InvalidOperationException("The Aspire AppHost has not been started.");
        }

        return App.GetApiDaprGrpcEndpoint();
    }

    public async Task<Uri> GetDashboardUrlAsync(CancellationToken cancellationToken = default)
    {
        if (App is null)
        {
            throw new InvalidOperationException("The Aspire AppHost has not been started.");
        }

        DashboardUrl ??= await App.GetDashboardUrlAsync(cancellationToken);
        return DashboardUrl!;
    }
}

public sealed record StoreInfo(string Id, string Name, string Description, decimal Price, int Quantity);
