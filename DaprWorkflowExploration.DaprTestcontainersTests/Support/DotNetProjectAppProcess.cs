using System.Diagnostics;

namespace DaprWorkflowExploration.DaprTestcontainersTests.Support;

internal sealed class DotNetProjectAppProcess : IAsyncDisposable
{
    private readonly Process process;
    private readonly int port;

    private DotNetProjectAppProcess(Process process, int port)
    {
        this.process = process;
        this.port = port;
    }

    public static DotNetProjectAppProcess Start(
        string solutionDirectory,
        string projectPath,
        int appPort,
        int daprHttpPort,
        int daprGrpcPort,
        string configuration = "Debug")
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = solutionDirectory,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };

        startInfo.ArgumentList.Add("run");
        startInfo.ArgumentList.Add("--project");
        startInfo.ArgumentList.Add(projectPath);
        startInfo.ArgumentList.Add("--no-build");
        startInfo.ArgumentList.Add("--configuration");
        startInfo.ArgumentList.Add(configuration);
        startInfo.ArgumentList.Add("--no-launch-profile");

        startInfo.Environment["ASPNETCORE_URLS"] = $"http://0.0.0.0:{appPort}";
        startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        startInfo.Environment["DOTNET_ENVIRONMENT"] = "Development";
        startInfo.Environment["DAPR_HTTP_ENDPOINT"] = $"http://127.0.0.1:{daprHttpPort}";
        startInfo.Environment["DAPR_GRPC_ENDPOINT"] = $"http://127.0.0.1:{daprGrpcPort}";

        var process = Process.Start(startInfo)
                      ?? throw new InvalidOperationException($"Failed to start project '{projectPath}'.");

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        return new DotNetProjectAppProcess(process, appPort);
    }

    public HttpClient CreateClient()
    {
        return new HttpClient
        {
            BaseAddress = new Uri($"http://127.0.0.1:{port}"),
            Timeout = Timeout.InfiniteTimeSpan
        };
    }

    public async Task WaitUntilHealthyAsync(string healthPath, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var client = CreateClient();
        var deadline = TimeProvider.System.GetTimestamp() + (long)(timeout.TotalSeconds * TimeProvider.System.TimestampFrequency);

        while (TimeProvider.System.GetTimestamp() < deadline)
        {
            if (process.HasExited)
            {
                throw new InvalidOperationException($"Project process exited before becoming healthy. Exit code: {process.ExitCode}.");
            }

            try
            {
                using var response = await client.GetAsync(healthPath, cancellationToken);

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

            await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);
        }

        throw new TimeoutException($"Project process did not become healthy at '{healthPath}' before the timeout.");
    }

    public async ValueTask DisposeAsync()
    {
        if (!process.HasExited)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
        }

        process.Dispose();
    }
}
