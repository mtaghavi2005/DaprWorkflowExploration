using Dapr.Client;
using Dapr.Workflow;

namespace DaprWorkflowExploration.ApiService.Activities;

internal sealed partial class UpdateInventoryActivity(ILogger<UpdateInventoryActivity> logger, DaprClient daprClient) : WorkflowActivity<PaymentRequest, object?>
{
    private const string StoreName = "statestore";

    public override async Task<object?> RunAsync(WorkflowActivityContext context, PaymentRequest req)
    {
        LogCheckingInventory(logger, req.RequestId, req.ItemBeingPurchased, req.Quantity);

        // Simulate slow processing
        await Task.Delay(TimeSpan.FromSeconds(5));

        // Determine if there are enough Items for purchase
        var (original, _) = await daprClient.GetStateAndETagAsync<StoreInfo>(StoreName, req.StoreId);

        if (original is null)
        {
            LogInsufficientInventory(logger, req.RequestId);
            throw new InvalidOperationException();
        }

        var newQuantity = original.Quantity - req.Quantity;
            
        if (newQuantity < 0)
        {
            LogInsufficientInventory(logger, req.RequestId);
            throw new InvalidOperationException();
        }

        await daprClient.SaveStateAsync(StoreName, req.StoreId, original with { Quantity = newQuantity });
        LogUpdatedInventory(logger, newQuantity, original.Name);

        return null;
    }

    [LoggerMessage(LogLevel.Information, "Checking inventory for request ID '{requestId}' for {quantity} {item}")]
    static partial void LogCheckingInventory(ILogger logger, string requestId, string item, int quantity);

    [LoggerMessage(LogLevel.Warning, "Payment for request ID '{requestId}' could not be processed as there's insufficient inventory available")]
    static partial void LogInsufficientInventory(ILogger logger, string requestId);

    [LoggerMessage(LogLevel.Information, "There are now {newQuantity} {itemName} left in stock")]
    static partial void LogUpdatedInventory(ILogger logger, int newQuantity, string itemName);
}
