using Dapr.Workflow;

namespace DaprWorkflowExploration.ApiService.Activities;

internal sealed partial class ProcessPaymentActivity(ILogger<ProcessPaymentActivity> logger) : WorkflowActivity<PaymentRequest, object?>
{
    public override async Task<object?> RunAsync(WorkflowActivityContext context, PaymentRequest req)
    {
        LogPaymentProcessing(logger, req.RequestId, req.Quantity, req.ItemBeingPurchased, req.TotalCost);

        // Simulate slow processing
        await Task.Delay(TimeSpan.FromSeconds(7));

        LogSuccessfulPayment(logger, req.RequestId);
        return null;
    }

    [LoggerMessage(LogLevel.Information, "Processing payment: request ID '{requestId}' for {quantity} {itemBeingPurchased} at ${totalCost}")]
    static partial void LogPaymentProcessing(ILogger logger, string requestId, int quantity, string itemBeingPurchased, decimal totalCost);

    [LoggerMessage(LogLevel.Information, "Payment for request ID '{requestId}' processed successfully")]
    static partial void LogSuccessfulPayment(ILogger logger, string requestId);
}
