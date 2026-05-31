using System.Net.Http.Json;
using DaprWorkflowExploration.IntegrationTests.TestUtils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Reqnroll;

namespace DaprWorkflowExploration.IntegrationTests.Steps;

[Binding]
public sealed class OrderWorkflowSteps(WorkflowScenarioContext context)
{
    [Given("the following store exists")]
    public async Task GivenTheFollowingStoreExists(Table table)
    {
        var row = table.Rows.Single();
        var store = new StoreInfo(
            Id: row["Id"],
            Name: row["Name"],
            Description: row["Description"],
            Price: decimal.Parse(row["Price"]),
            Quantity: int.Parse(row["Quantity"]));

        using var apiClient = context.CreateApiClient();
        using var response = await apiClient.PostAsJsonAsync("/store", store);

        Assert.AreEqual(System.Net.HttpStatusCode.Created, response.StatusCode);
    }

    [Then("store {string} should have {int} items remaining")]
    public async Task ThenStoreShouldHaveItemsRemaining(string storeId, int expectedQuantity)
    {
        using var apiClient = context.CreateApiClient();
        var store = await apiClient.GetFromJsonAsync<StoreInfo>($"/store/{storeId}");

        Assert.IsNotNull(store);
        Assert.AreEqual(expectedQuantity, store.Quantity);
    }
}
