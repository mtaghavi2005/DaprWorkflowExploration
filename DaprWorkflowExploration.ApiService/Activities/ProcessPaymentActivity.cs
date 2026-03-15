using System.Diagnostics;
using Dapr.Client;
using Dapr.Workflow;

namespace DaprWorkflowExploration.ApiService.Activities;

internal sealed partial class ProcessPaymentActivity(ILogger<ProcessPaymentActivity> logger, DaprClient daprClient) : WorkflowActivity<PaymentRequest, object?>
{
    private const string PubSubName = "pubsub";
    private const string PaymentRequestsTopic = "payment-requests";

    public override async Task<object?> RunAsync(WorkflowActivityContext context, PaymentRequest req)
    {
        LogActivityContext(
            logger,
            "ProcessPaymentActivity:before-publish",
            req.RequestId,
            Activity.Current?.TraceId.ToString(),
            Activity.Current?.SpanId.ToString(),
            Activity.Current?.ParentSpanId.ToString(),
            Activity.Current?.Id);

        LogPaymentRequested(logger, req.RequestId, req.Quantity, req.ItemBeingPurchased, req.TotalCost);

        var paymentRequest = new PaymentRequestedMessage(
            WorkflowInstanceId: req.RequestId,
            StoreId: req.StoreId,
            StoreName: req.ItemBeingPurchased,
            Quantity: req.Quantity,
            TotalCost: req.TotalCost);

        await daprClient.PublishEventAsync(PubSubName, PaymentRequestsTopic, paymentRequest);

        LogActivityContext(
            logger,
            "ProcessPaymentActivity:after-publish",
            req.RequestId,
            Activity.Current?.TraceId.ToString(),
            Activity.Current?.SpanId.ToString(),
            Activity.Current?.ParentSpanId.ToString(),
            Activity.Current?.Id);

        LogPublishedPaymentRequest(logger, req.RequestId);
        return null;
    }

    [LoggerMessage(LogLevel.Information, "Requesting async payment processing: request ID '{requestId}' for {quantity} {itemBeingPurchased} at ${totalCost}")]
    static partial void LogPaymentRequested(ILogger logger, string requestId, int quantity, string itemBeingPurchased, decimal totalCost);

    [LoggerMessage(LogLevel.Information, "Published payment request for workflow '{requestId}'")]
    static partial void LogPublishedPaymentRequest(ILogger logger, string requestId);

    [LoggerMessage(
        LogLevel.Information,
        "{location} workflow '{workflowInstanceId}' Activity.Current traceId={traceId} spanId={spanId} parentSpanId={parentSpanId} activityId={activityId}")]
    static partial void LogActivityContext(
        ILogger logger,
        string location,
        string workflowInstanceId,
        string? traceId,
        string? spanId,
        string? parentSpanId,
        string? activityId);
}
