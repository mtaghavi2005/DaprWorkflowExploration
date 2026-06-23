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

app.MapPost("/payment-authorizations",
        [Topic("pubsub", "payment-authorization-requests")] async (
            PaymentAuthorizationRequested request,
            DaprClient daprClient,
            ILogger<Program> logger,
            CancellationToken cancellationToken) =>
        {
            logger.LogInformation(
                "Accounting service received payment authorization request {PaymentId} for order {OrderId}: {CustomerId} {Currency} {Amount} Activity.Current traceId={TraceId} spanId={SpanId} parentSpanId={ParentSpanId} activityId={ActivityId}",
                request.PaymentId,
                request.OrderId,
                request.CustomerId,
                request.Currency,
                request.Amount,
                Activity.Current?.TraceId.ToString(),
                Activity.Current?.SpanId.ToString(),
                Activity.Current?.ParentSpanId.ToString(),
                Activity.Current?.Id);

            var result = new PaymentAuthorizationResult(
                PaymentId: request.PaymentId,
                OrderId: request.OrderId,
                CustomerId: request.CustomerId,
                Amount: request.Amount,
                Currency: request.Currency,
                Status: "Authorized",
                AuthorizationCode: $"auth_{Guid.NewGuid():N}"[..18],
                ProcessedAt: DateTimeOffset.UtcNow,
                Message: $"Payment {request.PaymentId} authorized.");

            await daprClient.PublishEventAsync("pubsub", "payment-authorization-results", result, cancellationToken);

            logger.LogInformation(
                "Accounting service published payment authorization result {PaymentId} Activity.Current traceId={TraceId} spanId={SpanId} parentSpanId={ParentSpanId} activityId={ActivityId}",
                request.PaymentId,
                Activity.Current?.TraceId.ToString(),
                Activity.Current?.SpanId.ToString(),
                Activity.Current?.ParentSpanId.ToString(),
                Activity.Current?.Id);

            return Results.Ok();
        })
    .WithName("HandlePaymentAuthorizationRequest");

app.MapDefaultEndpoints();

app.Run();

internal sealed record PaymentRequestedMessage(string WorkflowInstanceId, string StoreId, string StoreName, int Quantity, decimal TotalCost);
internal sealed record PaymentProcessedMessage(string WorkflowInstanceId, string StoreId, string StoreName, int Quantity, decimal TotalCost, bool Processed, string Message);
internal sealed record PaymentAuthorizationRequested(string PaymentId, string OrderId, string CustomerId, decimal Amount, string Currency, string PaymentMethodToken);
internal sealed record PaymentAuthorizationResult(string PaymentId, string OrderId, string CustomerId, decimal Amount, string Currency, string Status, string AuthorizationCode, DateTimeOffset ProcessedAt, string Message);
