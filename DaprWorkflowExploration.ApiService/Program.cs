using Dapr.Client;
using Dapr.Workflow;
using DaprWorkflowExploration.ApiService;
using DaprWorkflowExploration.ApiService.Activities;
using DaprWorkflowExploration.ApiService.Workflows;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);


// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddProblemDetails();
builder.Services.AddDaprClient();

const string storeName = "statestore";
const string storeIndexKey = "__store-index";

builder.Services.AddDaprWorkflow(options =>
{
    options.RegisterWorkflow<OrderProcessingWorkflow>();

    options.RegisterActivity<NotifyActivity>();
    options.RegisterActivity<VerifyInventoryActivity>();
    options.RegisterActivity<RequestApprovalActivity>();
    options.RegisterActivity<ProcessPaymentActivity>();
    options.RegisterActivity<UpdateInventoryActivity>();
});

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();


app.MapPost("/order/process", (OrderPayload orderPayload) => { });

app.MapGet("/store/{id}", async (string id, DaprClient daprClient, CancellationToken cancellationToken) =>
    {
        var storeInfo = await daprClient.GetStateAsync<StoreInfo?>(storeName, id, cancellationToken: cancellationToken);

        return storeInfo is null ? Results.NotFound() : Results.Ok(storeInfo);
    })
    .WithName("GetStoreInfo");

app.MapGet("/store", async (DaprClient daprClient, CancellationToken cancellationToken) =>
    {
        var storeIds = await daprClient.GetStateAsync<List<string>?>(storeName, storeIndexKey, cancellationToken: cancellationToken)
            ?? [];

        var stores = new List<StoreInfo>(storeIds.Count);

        foreach (var storeId in storeIds.Distinct(StringComparer.Ordinal))
        {
            var store = await daprClient.GetStateAsync<StoreInfo?>(storeName, storeId, cancellationToken: cancellationToken);

            if (store is not null)
            {
                stores.Add(store);
            }
        }

        return Results.Ok(stores);
    })
    .WithName("ListStoreInfo");

app.MapPost("/store",
        async ([FromBody] StoreInfo storeInfo, DaprClient daprClient, CancellationToken cancellationToken) =>
        {
            await daprClient.SaveStateAsync(storeName, storeInfo.Id, storeInfo, cancellationToken: cancellationToken);

            var storeIds = await daprClient.GetStateAsync<List<string>?>(storeName, storeIndexKey, cancellationToken: cancellationToken)
                ?? [];

            if (!storeIds.Contains(storeInfo.Id, StringComparer.Ordinal))
            {
                storeIds.Add(storeInfo.Id);
                await daprClient.SaveStateAsync(storeName, storeIndexKey, storeIds, cancellationToken: cancellationToken);
            }

            return Results.Created($"/store/{storeInfo.Id}", storeInfo);
        })
    .WithName("SaveStoreInfo");

app.MapGet("/dapr/metadata", async (DaprClient daprClient, CancellationToken cancellationToken) =>
    {
        var metadata = await daprClient.GetMetadataAsync(cancellationToken);

        return Results.Ok(new
        {
            metadata.Id,
            Actors = metadata.Actors.Select(actor => new
            {
                actor.Type,
                actor.Count
            }),
            Components = metadata.Components.Select(component => new
            {
                component.Name,
                component.Type,
                component.Version,
                component.Capabilities
            }),
            metadata.Extended
        });
    })
    .WithName("GetDaprMetadata");

app.MapDefaultEndpoints();

await app.RunAsync();
