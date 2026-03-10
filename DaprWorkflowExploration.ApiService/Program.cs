using Dapr.Client;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddProblemDetails();
builder.Services.AddDaprClient();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

string[] summaries =
    ["Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"];

app.MapGet("/", () => "API service is running. Navigate to /weatherforecast to see sample data.");

app.MapGet("/weatherforecast", () =>
    {
        var forecast = Enumerable.Range(1, 5).Select(index =>
                new WeatherForecast
                (
                    DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                    Random.Shared.Next(-20, 55),
                    summaries[Random.Shared.Next(summaries.Length)]
                ))
            .ToArray();
        return forecast;
    })
    .WithName("GetWeatherForecast");

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

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
