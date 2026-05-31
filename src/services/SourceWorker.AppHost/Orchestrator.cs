using System.Text.Json;
using Microsoft.Extensions.Logging;
using SourceWorker.Contracts;

namespace SourceWorker.AppHost;

public sealed class AppHostOrchestrator(
    IProcessLauncher processLauncher,
    IEndpointProbe endpointProbe,
    ILocalBrowserLauncher browserLauncher,
    ILogger<AppHostOrchestrator> logger) : IAppHostOrchestrator
{
    private ManagedProcessInfo? _apiProcess;
    private ManagedProcessInfo? _dashboardProcess;
    private AppHostOptions? _options;
    private DateTimeOffset _startedAt;

    public async Task StartAsync(AppHostOptions options, CancellationToken cancellationToken = default)
    {
        _options = options;
        _startedAt = DateTimeOffset.UtcNow;

        var solutionRoot = FindSolutionRoot();
        logger.LogInformation("Solution root found at: {Path}", solutionRoot);

        if (!options.DashboardOnly)
        {
            await StartApiAsync(solutionRoot, cancellationToken);
        }

        if (!options.ApiOnly)
        {
            await StartDashboardAsync(solutionRoot, cancellationToken);
        }

        if (options.OpenBrowser && _dashboardProcess?.BaseUrl != null)
        {
            logger.LogInformation("Opening browser at {Url}", _dashboardProcess.BaseUrl);
            browserLauncher.OpenBrowser(_dashboardProcess.BaseUrl);
        }
    }

    private async Task StartApiAsync(string solutionRoot, CancellationToken cancellationToken)
    {
        var projectPath = Path.Combine(solutionRoot, "src", "services", "SourceWorker.LocalApi", "SourceWorker.LocalApi.csproj");
        var args = $"run --project \"{projectPath}\"";
        
        if (_options?.ApiPort.HasValue == true)
        {
            args += $" -- SourceWorker:LocalApi:Port={_options.ApiPort}";
        }

        _apiProcess = await processLauncher.StartProcessAsync("LocalApi", "dotnet", args);
        
        // Default port is 5000 or from options
        var port = _options?.ApiPort ?? 5000;
        var baseUrl = $"http://localhost:{port}";
        
        logger.LogInformation("Waiting for Local API to become healthy at {Url}...", baseUrl);
        
        var healthy = false;
        for (int i = 0; i < 30; i++) // 30 seconds timeout
        {
            if (cancellationToken.IsCancellationRequested) break;
            
            var result = await endpointProbe.ProbeEndpointAsync($"{baseUrl}/health", cancellationToken);
            if (result.IsHealthy)
            {
                healthy = true;
                break;
            }
            await Task.Delay(1000, cancellationToken);
        }

        if (!healthy)
        {
            throw new Exception("Local API failed to start or become healthy within 30 seconds.");
        }

        _apiProcess = _apiProcess with { BaseUrl = baseUrl };
        logger.LogInformation("Local API is healthy.");
    }

    private async Task StartDashboardAsync(string solutionRoot, CancellationToken cancellationToken)
    {
        var projectPath = Path.Combine(solutionRoot, "ui", "SourceWorker.Dashboard", "SourceWorker.Dashboard.csproj");
        
        var envVars = new Dictionary<string, string>();
        if (_apiProcess?.BaseUrl != null)
        {
            envVars["SOURCEWORKER_API_BASE_URL"] = _apiProcess.BaseUrl;
            
            // Read token from the well-known location
            try
            {
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var tokenFilePath = Path.Combine(appData, "SourceWorker", "api-token.txt");
                if (File.Exists(tokenFilePath))
                {
                    var token = File.ReadAllText(tokenFilePath).Trim();
                    envVars["SOURCEWORKER_API_TOKEN"] = token;
                    logger.LogInformation("Passed API token to Dashboard via environment variable.");
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning("Could not read API token from well-known location: {Message}", ex.Message);
            }
        }

        envVars["SOURCEWORKER_LAUNCH_MODE"] = "AppHost";

        _dashboardProcess = await processLauncher.StartProcessAsync("Dashboard", "dotnet", $"run --project \"{projectPath}\"", envVars);
        
        // Dashboard usually runs on 5001 or 5002 in dev
        _dashboardProcess = _dashboardProcess with { BaseUrl = "http://localhost:5001" }; // Default Blazor dev port
    }

    public async Task StopAsync()
    {
        if (_dashboardProcess?.ProcessId.HasValue == true)
        {
            await processLauncher.StopProcessAsync(_dashboardProcess.ProcessId.Value);
        }
        if (_apiProcess?.ProcessId.HasValue == true)
        {
            await processLauncher.StopProcessAsync(_apiProcess.ProcessId.Value);
        }
    }

    public AppHostRuntimeState GetState()
    {
        return new AppHostRuntimeState(
            _apiProcess ?? new ManagedProcessInfo("LocalApi", null, ManagedProcessStatus.NotStarted, null, null),
            _dashboardProcess,
            _startedAt,
            "1.0.0" // TODO: Get real version
        );
    }

    private string FindSolutionRoot()
    {
        var current = Directory.GetCurrentDirectory();
        while (current != null)
        {
            if (Directory.GetFiles(current, "SourceWorker.slnx").Any() || Directory.GetFiles(current, "SourceWorker.sln").Any())
            {
                return current;
            }
            current = Path.GetDirectoryName(current);
        }
        return Directory.GetCurrentDirectory(); // Fallback
    }
}
