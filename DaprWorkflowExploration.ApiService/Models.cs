namespace DaprWorkflowExploration.ApiService;

public sealed record StoreInfo(string Id, string Name, string Description, decimal Price, int Quantity);

public sealed record OrderRequest(string StoreId, int Quantity);
public sealed record OrderSubmissionResponse(string WorkflowInstanceId, string StoreId, string StoreName, int Quantity, decimal TotalCost);
public sealed record OrderStatusResponse(string WorkflowInstanceId, string RuntimeStatus, bool IsCompleted, bool? Processed, string? Message);

internal sealed record OrderPayload(string StoreId, string StoreName, decimal UnitPrice, decimal TotalCost, int Quantity = 1);
internal sealed record InventoryRequest(string RequestId, string StoreId, int Quantity);
internal sealed record InventoryResult(bool Success, StoreInfo? Store);
internal sealed record ApprovalRequest(string RequestId, string ItemBeingPurchased, int Quantity, decimal Amount);
internal sealed record ApprovalResponse(string RequestId, bool IsApproved);
internal sealed record PaymentRequest(string RequestId, string StoreId, string ItemBeingPurchased, int Quantity, decimal TotalCost);
internal sealed record OrderResult(bool Processed, string Message);
