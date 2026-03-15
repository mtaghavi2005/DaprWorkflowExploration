using Dapr.Client;
using Dapr.Workflow;

namespace DaprWorkflowExploration.ApiService.Activities;

internal sealed partial class VerifyInventoryActivity(ILogger<VerifyInventoryActivity> logger, DaprClient daprClient) : WorkflowActivity<InventoryRequest, InventoryResult>
{
    private const string StoreName = "statestore";

    public override async Task<InventoryResult> RunAsync(WorkflowActivityContext context, InventoryRequest req)
    {
        LogVerifyInventory(logger, req.RequestId, req.Quantity, req.StoreId);

        // Ensure that the store has items
        var (storeInfo, _) = await daprClient.GetStateAndETagAsync<StoreInfo>(StoreName, req.StoreId);

        // Catch for the case where the statestore isn't setup
        if (storeInfo is null)
        {
            // Not enough items.
            LogStateNotFound(logger, req.RequestId, req.StoreId);
            return new InventoryResult(false, null);
        }

        // See if there are enough items to purchase
        if (storeInfo.Quantity >= req.Quantity)
        {
            // Simulate slow processing
            await Task.Delay(TimeSpan.FromSeconds(2));

            LogSufficientInventory(logger, storeInfo.Quantity, storeInfo.Name);
            return new InventoryResult(true, storeInfo);
        }

        // Not enough items.
        LogInsufficientInventory(logger, req.RequestId, storeInfo.Name);
        return new InventoryResult(false, storeInfo);
    }

    [LoggerMessage(LogLevel.Information, "Reserving inventory for order request ID '{requestId}' of {quantity} {name}")]
    static partial void LogVerifyInventory(ILogger logger, string requestId, int quantity, string name);

    [LoggerMessage(LogLevel.Warning, "Unable to locate an order result for request ID '{requestId}' for the indicated item {itemName} in the state store")]
    static partial void LogStateNotFound(ILogger logger, string requestId, string itemName);

    [LoggerMessage(LogLevel.Information, "There are: {quantity} {name} available for purchase")]
    static partial void LogSufficientInventory(ILogger logger, int quantity, string name);

    [LoggerMessage(LogLevel.Warning, "There is insufficient inventory available for order request ID '{requestId}' for the item {itemName}")]
    static partial void LogInsufficientInventory(ILogger logger, string requestId, string itemName);
}
