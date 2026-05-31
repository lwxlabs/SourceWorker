using Microsoft.Extensions.Logging;
using SourceWorker.Contracts;

namespace SourceWorker.Hosting;

public sealed class LocalApiLifecycleService(
    IProcessLauncher processLauncher,
    IEndpointProbe endpointProbe,
    IRuntimePathResolver paths,
    ILogger<LocalApiLifecycleService> logger) : ILocalApiLifecycleService
{
    private LocalApiLifecycleResult? current;

    public LocalApiLifecycleResult? Current => current;

    public async Task<LocalApiLifecycleResult> StartAsync(LocalApiLaunchOptions options, CancellationToken cancellationToken = default)
    {
        var startedUtc = DateTimeOffset.UtcNow;
        var steps = new List<string> { "locating Local API" };
        var port = options.ApiPort ?? 5000;
        var baseUrl = $"http://localhost:{port}";

        if (options.ReuseExisting)
        {
            steps.Add("checking existing Local API");
            var existing = await endpointProbe.ProbeEndpointAsync($"{baseUrl}/health", cancellationToken);
            if (existing.IsHealthy)
            {
                steps.Add("reusing existing Local API");
                current = new LocalApiLifecycleResult(
                    new ManagedProcessInfo("LocalApi", null, ManagedProcessStatus.Running, baseUrl, startedUtc),
                    StartedByOwner: false,
                    ReusedExisting: true,
                    baseUrl,
                    paths.ReadApiToken(),
                    startedUtc,
                    DateTimeOffset.UtcNow - startedUtc,
                    steps);
                return current;
            }
        }

        steps.Add("starting Local API");
        var solutionRoot = options.SolutionRoot ?? paths.FindSolutionRoot();
        var (command, arguments) = ResolveLocalApiCommand(solutionRoot, port);
        var process = await processLauncher.StartProcessAsync("LocalApi", command, arguments);

        steps.Add("waiting for health");
        var healthy = false;
        LocalEndpointProbeResult? lastProbe = null;
        for (var attempt = 0; attempt < 30; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lastProbe = await endpointProbe.ProbeEndpointAsync($"{baseUrl}/health", cancellationToken);
            if (lastProbe.IsHealthy)
            {
                healthy = true;
                break;
            }

            await Task.Delay(1000, cancellationToken);
        }

        if (!healthy)
        {
            var message = $"Local API failed to become healthy. Last probe: {lastProbe?.Message ?? "no response"}";
            logger.LogError("{Message}", message);
            current = new LocalApiLifecycleResult(process, true, false, baseUrl, null, startedUtc, DateTimeOffset.UtcNow - startedUtc, steps, message);
            throw new InvalidOperationException(message);
        }

        steps.Add("configuring token");
        var token = paths.ReadApiToken();
        current = new LocalApiLifecycleResult(
            process with { BaseUrl = baseUrl },
            StartedByOwner: true,
            ReusedExisting: false,
            baseUrl,
            token,
            startedUtc,
            DateTimeOffset.UtcNow - startedUtc,
            steps);
        return current;
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (current is { StartedByOwner: true, Process.ProcessId: int processId })
        {
            await processLauncher.StopProcessAsync(processId);
        }

        current = null;
    }

    private static (string Command, string Arguments) ResolveLocalApiCommand(string solutionRoot, int port)
    {
        var baseDirectory = AppContext.BaseDirectory;
        var publishedCandidates = new[]
        {
            Path.Combine(baseDirectory, "LocalApi", OperatingSystem.IsWindows() ? "SourceWorker.LocalApi.exe" : "SourceWorker.LocalApi"),
            Path.Combine(baseDirectory, OperatingSystem.IsWindows() ? "SourceWorker.LocalApi.exe" : "SourceWorker.LocalApi")
        };

        var published = publishedCandidates.FirstOrDefault(File.Exists);
        if (published is not null)
        {
            return (published, $"SourceWorker:LocalApi:Port={port}");
        }

        var projectPath = Path.Combine(solutionRoot, "src", "services", "SourceWorker.LocalApi", "SourceWorker.LocalApi.csproj");
        return ("dotnet", $"run --project \"{projectPath}\" -- SourceWorker:LocalApi:Port={port}");
    }
}
