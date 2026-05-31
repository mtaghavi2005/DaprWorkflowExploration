using System.Reflection;
using Aspire.Hosting;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DaprWorkflowExploration.IntegrationTests.TestUtils;

public sealed class AspireAppFixture : IAsyncDisposable
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(2);
    private static readonly string[] AppResources = ["accountingservice", "apiservice"];
    private static readonly TimeSpan HealthProbeDelay = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan HealthProbeTimeout = TimeSpan.FromSeconds(2);

    private DistributedApplication? app;

    public HttpClient CreateApiClient()
    {
        if (app is null)
        {
            throw new InvalidOperationException("The Aspire AppHost has not been started.");
        }

        var httpClient = app.CreateHttpClient("apiservice");
        httpClient.Timeout = Timeout.InfiniteTimeSpan;

        return httpClient;
    }

    public async Task<Uri> GetDashboardUrlAsync(CancellationToken cancellationToken = default)
    {
        if (app is null)
        {
            throw new InvalidOperationException("The Aspire AppHost has not been started.");
        }

        var loggerFactory = app.Services.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger("AspireDashboard");
        var dashboardInfo = await GetDashboardConnectionInfoAsync(logger, cancellationToken);

        var urlValue = dashboardInfo
            .GetType()
            .GetProperty("BaseUrlWithLoginToken", BindingFlags.Public | BindingFlags.Instance)
            ?.GetValue(dashboardInfo) as string;

        if (!Uri.TryCreate(urlValue, UriKind.Absolute, out var dashboardUrl))
        {
            throw new InvalidOperationException("Aspire dashboard URL was not available.");
        }

        return dashboardUrl;
    }

    public async Task StartAsync()
    {
        if (app is not null)
        {
            return;
        }

        using var cts = new CancellationTokenSource(DefaultTimeout);
        var cancellationToken = cts.Token;

        var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.DaprWorkflowExploration_AppHost>(
            [],
            (appOptions, _) =>
            {
                appOptions.DisableDashboard = false;
                appOptions.AllowUnsecuredTransport = true;
                appOptions.TrustDeveloperCertificate = false;
                appOptions.DeveloperCertificateDefaultHttpsTerminationEnabled = false;
            },
            cancellationToken);

        builder.Services.AddLogging(logging =>
        {
            logging.SetMinimumLevel(LogLevel.Information);
            logging.AddFilter("Aspire.Hosting.Dcp", LogLevel.Warning);
        });

        builder.Services.ConfigureHttpClientDefaults(clientBuilder =>
        {
            clientBuilder.ConfigureHttpClient(httpClient => { httpClient.Timeout = Timeout.InfiniteTimeSpan; });
        });

        app = await builder.BuildAsync(cancellationToken).WaitAsync(DefaultTimeout, cancellationToken);
        await app.StartAsync(cancellationToken).WaitAsync(DefaultTimeout, cancellationToken);

        await WaitForAppResourcesAsync(cancellationToken);
        await WaitForApiHealthAsync(cancellationToken);
    }

    private async Task<object> GetDashboardConnectionInfoAsync(ILogger logger, CancellationToken cancellationToken)
    {
        if (app is null)
        {
            throw new InvalidOperationException("The Aspire AppHost has not been started.");
        }

        var helperType = typeof(DistributedApplication).Assembly
            .GetType("Aspire.Hosting.Backchannel.DashboardUrlsHelper");

        var method = helperType?.GetMethod(
            "GetDashboardConnectionInfoAsync",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
            binder: null,
            [typeof(IServiceProvider), typeof(ILogger), typeof(CancellationToken)],
            modifiers: null);

        if (method is null)
        {
            throw new InvalidOperationException(
                "Aspire dashboard URL helper was not available. Ensure Aspire.Hosting.Testing is 13.2 or newer.");
        }

        if (method.Invoke(null, [app.Services, logger, cancellationToken]) is not Task dashboardTask)
        {
            throw new InvalidOperationException("Aspire dashboard URL helper returned an unexpected result.");
        }

        await dashboardTask.WaitAsync(DefaultTimeout, cancellationToken);

        var result = dashboardTask
            .GetType()
            .GetProperty("Result", BindingFlags.Public | BindingFlags.Instance)
            ?.GetValue(dashboardTask);

        return result ??
               throw new InvalidOperationException(
                   "Aspire dashboard URL helper returned no dashboard connection information.");
    }

    private async Task WaitForAppResourcesAsync(CancellationToken cancellationToken)
    {
        if (app is null)
        {
            throw new InvalidOperationException("The Aspire AppHost has not been started.");
        }

        foreach (var resourceName in AppResources)
        {
            await app.ResourceNotifications
                .WaitForResourceHealthyAsync(resourceName, cancellationToken)
                .WaitAsync(DefaultTimeout, cancellationToken);
        }
    }

    private async Task WaitForApiHealthAsync(CancellationToken cancellationToken)
    {
        if (app is null)
        {
            throw new InvalidOperationException("The Aspire AppHost has not been started.");
        }

        using var readinessClient = app.CreateHttpClient("apiservice");
        readinessClient.Timeout = HealthProbeTimeout;

        var deadline = TimeProvider.System.GetTimestamp() + (long)(DefaultTimeout.TotalSeconds * TimeProvider.System.TimestampFrequency);

        while (TimeProvider.System.GetTimestamp() < deadline)
        {
            try
            {
                using var response = await readinessClient.GetAsync("/health", cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch (HttpRequestException)
            {
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
            }

            await Task.Delay(HealthProbeDelay, cancellationToken);
        }

        throw new TimeoutException("The apiservice /health endpoint did not become ready before the timeout.");
    }

    public async ValueTask DisposeAsync()
    {
        if (app is not null)
        {
            await app.DisposeAsync();
        }
    }
}
