using Reqnroll;

namespace DaprWorkflowExploration.IntegrationTests.TestUtils;

[Binding]
public sealed class AspireAppHooks(WorkflowScenarioContext context)
{
    [BeforeScenario(Order = 0)]
    public async Task StartAppHostAsync()
    {
        context.App = new AspireAppFixture();
        await context.App.StartAsync();
        context.DashboardUrl = await context.App.GetDashboardUrlAsync();

        Console.WriteLine($"Aspire dashboard: {context.DashboardUrl}");
    }

    [AfterScenario(Order = 100)]
    public async Task StopAppHostAsync()
    {
        if (context.App is not null)
        {
            await context.App.DisposeAsync();
        }
    }
}
