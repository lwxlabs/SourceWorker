using Microsoft.Extensions.Logging;
using SourceWorker.Contracts;

namespace SourceWorker.Hosting;

public sealed class AppHostOrchestrator(
    ILocalApiLifecycleService localApi,
    IProcessLauncher processLauncher,
    IRuntimePathResolver paths,
    ILocalBrowserLauncher browserLauncher,
    ILogger<AppHostOrchestrator> logger) : IAppHostOrchestrator
{
    private ManagedProcessInfo? dashboardProcess;
    private DateTimeOffset startedAt;

    public async Task StartAsync(AppHostOptions options, CancellationToken cancellationToken = default)
    {
        startedAt = DateTimeOffset.UtcNow;
        var solutionRoot = paths.FindSolutionRoot();
        logger.LogInformation("Solution root found at: {Path}", solutionRoot);

        LocalApiLifecycleResult? api = null;
        if (!options.DashboardOnly)
        {
            api = await localApi.StartAsync(new LocalApiLaunchOptions(options.ApiPort, solutionRoot, ReuseExisting: false, LaunchMode.AppHost), cancellationToken);
        }

        if (!options.ApiOnly)
        {
            dashboardProcess = await StartDashboardAsync(solutionRoot, api, cancellationToken);
        }

        if (options.OpenBrowser && dashboardProcess?.BaseUrl != null)
        {
            logger.LogInformation("Opening browser at {Url}", dashboardProcess.BaseUrl);
            browserLauncher.OpenBrowser(dashboardProcess.BaseUrl);
        }
    }

    private async Task<ManagedProcessInfo> StartDashboardAsync(string solutionRoot, LocalApiLifecycleResult? api, CancellationToken cancellationToken)
    {
        var projectPath = Path.Combine(solutionRoot, "ui", "SourceWorker.Dashboard", "SourceWorker.Dashboard.csproj");
        var envVars = new Dictionary<string, string> { ["SOURCEWORKER_LAUNCH_MODE"] = "AppHost" };
        if (api is not null)
        {
            envVars["SOURCEWORKER_API_BASE_URL"] = api.BaseUrl;
            if (!string.IsNullOrWhiteSpace(api.Token))
            {
                envVars["SOURCEWORKER_API_TOKEN"] = api.Token;
            }
        }

        var process = await processLauncher.StartProcessAsync("Dashboard", "dotnet", $"run --project \"{projectPath}\"", envVars);
        return process with { BaseUrl = "http://localhost:5001" };
    }

    public async Task StopAsync()
    {
        if (dashboardProcess?.ProcessId is int dashboardPid)
        {
            await processLauncher.StopProcessAsync(dashboardPid);
        }

        await localApi.StopAsync();
    }

    public AppHostRuntimeState GetState()
        => new(
            localApi.Current?.Process ?? new ManagedProcessInfo("LocalApi", null, ManagedProcessStatus.NotStarted, null, null),
            dashboardProcess,
            startedAt,
            "1.0.0");
}
