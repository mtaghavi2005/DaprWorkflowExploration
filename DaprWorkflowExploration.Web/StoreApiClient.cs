using Dapr.Client;
using System.ComponentModel.DataAnnotations;

namespace DaprWorkflowExploration.Web;

public sealed class StoreApiClient(DaprClient daprClient)
{
    public async Task<IReadOnlyList<StoreInfo>> GetStoresAsync(CancellationToken cancellationToken = default)
    {
        var client = daprClient.CreateInvokableHttpClient("apiservice");
        return await client.GetFromJsonAsync<List<StoreInfo>>("/store", cancellationToken) ?? [];
    }

    public async Task<StoreInfo?> GetStoreAsync(string id, CancellationToken cancellationToken = default)
    {
        var client = daprClient.CreateInvokableHttpClient("apiservice");
        return await client.GetFromJsonAsync<StoreInfo?>($"/store/{id}", cancellationToken);
    }
        

    public async Task SaveStoreAsync(StoreInfo storeInfo, CancellationToken cancellationToken = default)
    {
        var client = daprClient.CreateInvokableHttpClient("apiservice");
        await client.PostAsJsonAsync("/store", storeInfo, cancellationToken);
    }
        
}

public sealed class StoreInfo
{
    [Required]
    public string Id { get; set; } = string.Empty;

    [Required]
    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    [Range(typeof(decimal), "0", "79228162514264337593543950335")]
    public decimal Price { get; set; }

    [Range(0, int.MaxValue)]
    public int Quantity { get; set; }
}
