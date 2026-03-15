using System.Diagnostics;
using Dapr.Client;
using Dapr;
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
app.UseCloudEvents();

app.MapSubscribeHandler();


app.MapPost("/order/process",
        async ([FromBody] OrderRequest orderRequest, DaprClient daprClient, DaprWorkflowClient workflowClient, CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(orderRequest.StoreId))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["StoreId"] = ["Store selection is required."]
                });
            }

            if (orderRequest.Quantity <= 0)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["Quantity"] = ["Quantity must be greater than zero."]
                });
            }

            var storeInfo = await daprClient.GetStateAsync<StoreInfo?>(storeName, orderRequest.StoreId, cancellationToken: cancellationToken);

            if (storeInfo is null)
            {
                return Results.NotFound(new { Message = $"Store '{orderRequest.StoreId}' was not found." });
            }

            var workflowPayload = new OrderPayload(
                StoreId: storeInfo.Id,
                StoreName: storeInfo.Name,
                UnitPrice: storeInfo.Price,
                TotalCost: storeInfo.Price * orderRequest.Quantity,
                Quantity: orderRequest.Quantity);

            var workflowInstanceId = await workflowClient.ScheduleNewWorkflowAsync(
                nameof(OrderProcessingWorkflow),
                instanceId: null,
                input: workflowPayload,
                startTime: null,
                cancellation: cancellationToken);

            return Results.Accepted(
                $"/order/process/{workflowInstanceId}",
                new OrderSubmissionResponse(workflowInstanceId, storeInfo.Id, storeInfo.Name, orderRequest.Quantity, workflowPayload.TotalCost));
        })
    .WithName("ProcessStoreOrder");

app.MapGet("/order/process/{workflowInstanceId}",
        async (string workflowInstanceId, DaprWorkflowClient workflowClient, CancellationToken cancellationToken) =>
        {
            var workflowState = await workflowClient.GetWorkflowStateAsync(workflowInstanceId, cancellation: cancellationToken);

            if (workflowState is null || !workflowState.Exists)
            {
                return Results.NotFound(new { Message = $"Workflow '{workflowInstanceId}' was not found." });
            }

            var result = workflowState.ReadOutputAs<OrderResult>();

            return Results.Ok(new OrderStatusResponse(
                WorkflowInstanceId: workflowInstanceId,
                RuntimeStatus: workflowState.RuntimeStatus.ToString(),
                IsCompleted: workflowState.IsWorkflowCompleted,
                Processed: result?.Processed,
                Message: result?.Message));
        })
    .WithName("GetOrderStatus");

app.MapPost("/payment-results",
        [Topic("pubsub", "payment-results")] async ([FromBody] PaymentProcessedMessage paymentProcessedMessage,
            DaprWorkflowClient workflowClient,
            ILogger<Program> logger,
            CancellationToken cancellationToken) =>
        {
            logger.LogInformation(
                "payment-results subscriber received workflow '{WorkflowInstanceId}' Activity.Current traceId={TraceId} spanId={SpanId} parentSpanId={ParentSpanId} activityId={ActivityId}",
                paymentProcessedMessage.WorkflowInstanceId,
                Activity.Current?.TraceId.ToString(),
                Activity.Current?.SpanId.ToString(),
                Activity.Current?.ParentSpanId.ToString(),
                Activity.Current?.Id);

            await workflowClient.RaiseEventAsync(
                paymentProcessedMessage.WorkflowInstanceId,
                "PaymentProcessedEvent",
                paymentProcessedMessage,
                cancellationToken);

            logger.LogInformation(
                "RaiseEventAsync completed for workflow '{WorkflowInstanceId}' Activity.Current traceId={TraceId} spanId={SpanId} parentSpanId={ParentSpanId} activityId={ActivityId}",
                paymentProcessedMessage.WorkflowInstanceId,
                Activity.Current?.TraceId.ToString(),
                Activity.Current?.SpanId.ToString(),
                Activity.Current?.ParentSpanId.ToString(),
                Activity.Current?.Id);

            return Results.Ok();
        })
    .WithName("ProcessPaymentResult");

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
