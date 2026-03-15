using Dapr.Workflow;

namespace DaprWorkflowExploration.ApiService.Activities;

internal sealed partial class RequestApprovalActivity(ILogger<RequestApprovalActivity> logger) : WorkflowActivity<ApprovalRequest, object?>
{
    public override async Task<object?> RunAsync(WorkflowActivityContext context, ApprovalRequest approvalRequest)
    {
        LogRequestApproval(logger, approvalRequest);

        // Simulate slow processing & sending the approval to the recipient
        await Task.Delay(TimeSpan.FromSeconds(2));

        return Task.FromResult<object?>(null);
    }
    
    [LoggerMessage(LogLevel.Information, "Approval Request {approvalRequest}")]
    static partial void LogRequestApproval(ILogger logger, ApprovalRequest approvalRequest);
}