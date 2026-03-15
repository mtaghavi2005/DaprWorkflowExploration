using Dapr.Workflow;
using DaprWorkflowExploration.ApiService.Activities;

namespace DaprWorkflowExploration.ApiService.Workflows;

internal sealed partial class OrderProcessingWorkflow : Workflow<OrderPayload, OrderResult>
{
    public override async Task<OrderResult> RunAsync(WorkflowContext context, OrderPayload order)
    {
        var logger = context.CreateReplaySafeLogger<OrderProcessingWorkflow>();
        var orderId = context.InstanceId;

        // Notify the user that an order has come through
        await context.CallActivityAsync(nameof(NotifyActivity),
            new Notification($"Received order {orderId} for {order.Quantity} {order.StoreName} at ${order.TotalCost}"));
        LogOrderReceived(logger, orderId, order.Quantity, order.StoreName, order.TotalCost);

        // Determine if there is enough of the item available for purchase by checking the inventory
        var inventoryRequest = new InventoryRequest(RequestId: orderId, order.StoreId, order.Quantity);
        var result = await context.CallActivityAsync<InventoryResult>(
            nameof(VerifyInventoryActivity), inventoryRequest);
        LogCheckInventory(logger, inventoryRequest);
            
        // If there is insufficient inventory, fail and let the user know 
        if (!result.Success)
        {
            // End the workflow here since we don't have sufficient inventory
            await context.CallActivityAsync(nameof(NotifyActivity),
                new Notification($"Insufficient inventory for {order.StoreName}"));
            LogInsufficientInventory(logger, order.StoreName);
            return new OrderResult(Processed: false, Message: $"Insufficient inventory for {order.StoreName}.");
        }

        if (order.TotalCost > 5000m)
        {
            await context.CallActivityAsync(nameof(RequestApprovalActivity),
                new ApprovalRequest(orderId, order.StoreName, order.Quantity, order.TotalCost));

            var approvalResponse = await context.WaitForExternalEventAsync<ApprovalResponse>(
                eventName: "ApprovalEvent",
                timeout: TimeSpan.FromSeconds(30));
            if (!approvalResponse.IsApproved)
            {
                await context.CallActivityAsync(nameof(NotifyActivity),
                    new Notification($"Order {orderId} was not approved"));
                LogOrderNotApproved(logger, orderId);
                return new OrderResult(Processed: false, Message: $"Order {orderId} was not approved.");
            }
        }

        // There is enough inventory available so the user can purchase the item(s). Process their payment
        var processPaymentRequest = new PaymentRequest(orderId, order.StoreId, order.StoreName, order.Quantity, order.TotalCost);
        await context.CallActivityAsync(nameof(ProcessPaymentActivity), processPaymentRequest);
        LogPaymentRequested(logger, processPaymentRequest);

        var paymentResult = await context.WaitForExternalEventAsync<PaymentProcessedMessage>(
            eventName: "PaymentProcessedEvent",
            timeout: TimeSpan.FromSeconds(60));

        if (!paymentResult.Processed)
        {
            await context.CallActivityAsync(nameof(NotifyActivity),
                new Notification(paymentResult.Message));
            LogPaymentRejected(logger, orderId);
            return new OrderResult(Processed: false, Message: paymentResult.Message);
        }

        try
        {
            // Update the available inventory
            var paymentRequest = new PaymentRequest(orderId, order.StoreId, order.StoreName, order.Quantity, order.TotalCost);
            await context.CallActivityAsync(nameof(UpdateInventoryActivity), paymentRequest);
            LogInventoryUpdate(logger, paymentRequest);
        }
        catch (WorkflowTaskFailedException)
        {
            // Let them know their payment was processed, but there's insufficient inventory, so they're getting a refund
            await context.CallActivityAsync(nameof(NotifyActivity),
                new Notification($"Order {orderId} Failed! You are now getting a refund"));
            LogRefund(logger, orderId);
            return new OrderResult(Processed: false, Message: $"Order {orderId} failed after payment and is being refunded.");
        }

        // Let them know their payment was processed
        await context.CallActivityAsync(nameof(NotifyActivity), new Notification($"Order {orderId} has completed!"));
        LogSuccessfulOrder(logger, orderId);

        // End the workflow with a success result
        return new OrderResult(Processed: true, Message: $"Order {orderId} for {order.StoreName} has completed.");
    }

    [LoggerMessage(LogLevel.Information, "Received request ID '{request}' for {quantity} {name} at ${totalCost}")]
    static partial void LogOrderReceived(ILogger logger, string request, int quantity, string name, decimal totalCost);
    
    [LoggerMessage(LogLevel.Information, "Checked inventory for request ID '{request}'")]
    static partial void LogCheckInventory(ILogger logger, InventoryRequest request);
    
    [LoggerMessage(LogLevel.Information, "Insufficient inventory for order {orderName}")]
    static partial void LogInsufficientInventory(ILogger logger, string orderName);
    
    [LoggerMessage(LogLevel.Information, "Order {orderName} was not approved")]
    static partial void LogOrderNotApproved(ILogger logger, string orderName);

    [LoggerMessage(LogLevel.Information, "Published payment request for async processing: {request}")]
    static partial void LogPaymentRequested(ILogger logger, PaymentRequest request);

    [LoggerMessage(LogLevel.Information, "Order {orderId} payment was rejected")]
    static partial void LogPaymentRejected(ILogger logger, string orderId);

    [LoggerMessage(LogLevel.Information, "Updating available inventory for {request}")]
    static partial void LogInventoryUpdate(ILogger logger, PaymentRequest request);

    [LoggerMessage(LogLevel.Information, "Order {orderId} failed due to insufficient inventory - processing refund")]
    static partial void LogRefund(ILogger logger, string orderId);

    [LoggerMessage(LogLevel.Information, "Order {orderId} has completed")]
    static partial void LogSuccessfulOrder(ILogger logger, string orderId);
}
