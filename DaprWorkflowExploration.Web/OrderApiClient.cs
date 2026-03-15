using Dapr.Client;
using System.ComponentModel.DataAnnotations;

namespace DaprWorkflowExploration.Web;

public sealed class OrderApiClient(DaprClient daprClient)
{
    public async Task<OrderSubmissionResponse> SubmitOrderAsync(OrderRequest orderRequest, CancellationToken cancellationToken = default)
    {
        var client = daprClient.CreateInvokableHttpClient("apiservice");
        using var response = await client.PostAsJsonAsync("/order/process", orderRequest, cancellationToken);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<OrderSubmissionResponse>(cancellationToken))!;
    }

    public async Task<OrderStatusResponse?> GetOrderStatusAsync(string workflowInstanceId, CancellationToken cancellationToken = default)
    {
        var client = daprClient.CreateInvokableHttpClient("apiservice");
        return await client.GetFromJsonAsync<OrderStatusResponse>($"/order/process/{workflowInstanceId}", cancellationToken);
    }
}

public sealed class OrderRequest
{
    [Required]
    public string StoreId { get; set; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int Quantity { get; set; } = 1;
}

public sealed class OrderSubmissionResponse(string workflowInstanceId, string storeId, string storeName, int quantity, decimal totalCost)
{
    public string WorkflowInstanceId { get; init; } = workflowInstanceId;
    public string StoreId { get; init; } = storeId;
    public string StoreName { get; init; } = storeName;
    public int Quantity { get; init; } = quantity;
    public decimal TotalCost { get; init; } = totalCost;
}

public sealed class OrderStatusResponse(string workflowInstanceId, string runtimeStatus, bool isCompleted, bool? processed, string? message)
{
    public string WorkflowInstanceId { get; init; } = workflowInstanceId;
    public string RuntimeStatus { get; init; } = runtimeStatus;
    public bool IsCompleted { get; init; } = isCompleted;
    public bool? Processed { get; init; } = processed;
    public string? Message { get; init; } = message;
}
