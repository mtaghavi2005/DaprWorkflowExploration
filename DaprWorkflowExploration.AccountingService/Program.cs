using System.Diagnostics;
using Dapr;
using Dapr.Client;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddProblemDetails();
builder.Services.AddDaprClient();

var app = builder.Build();

app.UseExceptionHandler();
app.UseCloudEvents();

app.MapSubscribeHandler();

app.MapPost("/payment-requests",
        [Topic("pubsub", "payment-requests")] async (
            PaymentRequestedMessage paymentRequestedMessage,
            DaprClient daprClient,
            ILogger<Program> logger,
            CancellationToken cancellationToken) =>
        {
            logger.LogInformation(
                "Accounting service received payment request for workflow {WorkflowInstanceId}: {Quantity} {StoreName} at ${TotalCost}",
                paymentRequestedMessage.WorkflowInstanceId,
                paymentRequestedMessage.Quantity,
                paymentRequestedMessage.StoreName,
                paymentRequestedMessage.TotalCost);

            logger.LogInformation(
                "Accounting subscriber Activity.Current traceId={TraceId} spanId={SpanId} parentSpanId={ParentSpanId} activityId={ActivityId}",
                Activity.Current?.TraceId.ToString(),
                Activity.Current?.SpanId.ToString(),
                Activity.Current?.ParentSpanId.ToString(),
                Activity.Current?.Id);

            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);

            var processedMessage = new PaymentProcessedMessage(
                WorkflowInstanceId: paymentRequestedMessage.WorkflowInstanceId,
                StoreId: paymentRequestedMessage.StoreId,
                StoreName: paymentRequestedMessage.StoreName,
                Quantity: paymentRequestedMessage.Quantity,
                TotalCost: paymentRequestedMessage.TotalCost,
                Processed: true,
                Message: $"Payment processed for {paymentRequestedMessage.StoreName}.");

            await daprClient.PublishEventAsync("pubsub", "payment-results", processedMessage, cancellationToken);

            logger.LogInformation(
                "Accounting service published payment result for workflow {WorkflowInstanceId} Activity.Current traceId={TraceId} spanId={SpanId} parentSpanId={ParentSpanId} activityId={ActivityId}",
                paymentRequestedMessage.WorkflowInstanceId,
                Activity.Current?.TraceId.ToString(),
                Activity.Current?.SpanId.ToString(),
                Activity.Current?.ParentSpanId.ToString(),
                Activity.Current?.Id);

            return Results.Ok();
        })
    .WithName("HandlePaymentRequest");

app.MapDefaultEndpoints();

app.Run();

internal sealed record PaymentRequestedMessage(string WorkflowInstanceId, string StoreId, string StoreName, int Quantity, decimal TotalCost);
internal sealed record PaymentProcessedMessage(string WorkflowInstanceId, string StoreId, string StoreName, int Quantity, decimal TotalCost, bool Processed, string Message);
