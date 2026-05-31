using System.Text.Json;
using System.Text;
using Dapr.DurableTask.Protobuf;
using DaprWorkflowExploration.IntegrationTests.TestUtils;
using Grpc.Net.Client;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Reqnroll;

namespace DaprWorkflowExploration.IntegrationTests.Steps;

[Binding]
public sealed class WorkflowSteps(WorkflowScenarioContext context)
{
    [When("I post JSON to {string} and capture workflow instance id from {string}")]
    public async Task WhenIPostJsonToAndCaptureWorkflowInstanceIdFrom(string path, string instanceIdPropertyName, string requestJson)
    {
        using var apiClient = context.CreateApiClient();
        using var request = new StringContent(
            WorkflowText.ExpandTokens(requestJson, context),
            Encoding.UTF8,
            "application/json");

        using var response = await apiClient.PostAsync(WorkflowText.ExpandTokens(path, context), request);
        var responseJson = await response.Content.ReadAsStringAsync();

        Assert.IsTrue(
            response.IsSuccessStatusCode,
            $"POST '{path}' failed with status {(int)response.StatusCode}: {responseJson}");

        using var document = JsonDocument.Parse(responseJson);
        Assert.IsTrue(
            document.RootElement.TryGetProperty(instanceIdPropertyName, out var instanceIdElement),
            $"Response did not contain workflow instance ID property '{instanceIdPropertyName}'.");

        context.WorkflowInstanceId = instanceIdElement.GetString();
        Assert.IsFalse(string.IsNullOrWhiteSpace(context.WorkflowInstanceId));
    }

    [Then("the JSON response from {string} should contain within {int} seconds")]
    public async Task ThenTheJsonResponseFromShouldContainWithinSeconds(string path, int timeoutSeconds, string expectedJson)
    {
        var expandedPath = WorkflowText.ExpandTokens(path, context);
        var expandedExpectedJson = WorkflowText.ExpandTokens(expectedJson, context);
        var deadline = DateTimeOffset.UtcNow.AddSeconds(timeoutSeconds);
        string? lastResponseJson = null;

        using var apiClient = context.CreateApiClient();

        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await apiClient.GetAsync(expandedPath);
            lastResponseJson = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode && JsonAssert.TryContainsJson(lastResponseJson, expandedExpectedJson))
            {
                return;
            }

            await Task.Delay(TimeSpan.FromSeconds(1));
        }

        Assert.Fail($"GET '{expandedPath}' did not contain expected JSON within {timeoutSeconds} seconds. Last response: {lastResponseJson}");
    }

    [Then("workflow activity history should contain in order within {int} seconds")]
    public async Task ThenWorkflowActivityHistoryShouldContainInOrderWithinSeconds(int timeoutSeconds, Table table)
    {
        var expectedStepNames = table.Rows.Select(row => row["Name"]).ToArray();
        var deadline = DateTimeOffset.UtcNow.AddSeconds(timeoutSeconds);
        string[] lastStepNames = [];

        Assert.IsFalse(
            string.IsNullOrWhiteSpace(context.WorkflowInstanceId),
            "No workflow instance ID has been captured for this scenario.");

        using var channel = GrpcChannel.ForAddress(context.GetApiDaprGrpcEndpoint());
        var workflowClient = new TaskHubSidecarService.TaskHubSidecarServiceClient(channel);

        while (DateTimeOffset.UtcNow < deadline)
        {
            var history = await workflowClient.GetInstanceHistoryAsync(new GetInstanceHistoryRequest
            {
                InstanceId = context.WorkflowInstanceId
            });

            lastStepNames = ReadWorkflowActivityNames(history.Events);

            if (ContainsInOrder(lastStepNames, expectedStepNames))
            {
                return;
            }

            await Task.Delay(TimeSpan.FromSeconds(1));
        }

        Assert.Fail(
            $"Workflow history did not contain the expected steps in order within {timeoutSeconds} seconds. " +
            $"Expected: {string.Join(", ", expectedStepNames)}. " +
            $"Actual: {string.Join(", ", lastStepNames)}.");
    }

    private static string[] ReadWorkflowActivityNames(IEnumerable<HistoryEvent> historyEvents)
    {
        return historyEvents
            .OrderBy(historyEvent => historyEvent.EventId)
            .Where(historyEvent => historyEvent.EventTypeCase is HistoryEvent.EventTypeOneofCase.TaskScheduled)
            .Select(historyEvent => historyEvent.TaskScheduled.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToArray();
    }

    private static bool ContainsInOrder(IReadOnlyList<string> actual, IReadOnlyList<string> expected)
    {
        if (expected.Count == 0)
        {
            return true;
        }

        var expectedIndex = 0;

        foreach (var actualStep in actual)
        {
            if (string.Equals(actualStep, expected[expectedIndex], StringComparison.Ordinal))
            {
                expectedIndex++;

                if (expectedIndex == expected.Count)
                {
                    return true;
                }
            }
        }

        return false;
    }
}
