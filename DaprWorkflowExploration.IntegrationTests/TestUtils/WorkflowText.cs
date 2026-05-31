namespace DaprWorkflowExploration.IntegrationTests.TestUtils;

internal static class WorkflowText
{
    public static string ExpandTokens(string text, WorkflowScenarioContext context)
    {
        if (!text.Contains("{{workflowInstanceId}}", StringComparison.Ordinal))
        {
            return text;
        }

        return text.Replace(
            "{{workflowInstanceId}}",
            context.WorkflowInstanceId ?? throw new InvalidOperationException("No workflow instance ID has been captured."));
    }
}
