using SourceWorker.Contracts;

namespace SourceWorker.AppHost;

public interface IProcessLauncher
{
    Task<ManagedProcessInfo> StartProcessAsync(string name, string command, string arguments, IDictionary<string, string>? environmentVariables = null);
    Task StopProcessAsync(int processId);
    bool IsProcessRunning(int processId);
}

public interface IEndpointProbe
{
    Task<LocalEndpointProbeResult> ProbeEndpointAsync(string url, CancellationToken cancellationToken = default);
}

public interface IAppHostOrchestrator
{
    Task StartAsync(AppHostOptions options, CancellationToken cancellationToken = default);
    Task StopAsync();
    AppHostRuntimeState GetState();
}

public interface ILocalBrowserLauncher
{
    void OpenBrowser(string url);
}

public sealed record AppHostOptions(
    bool ApiOnly = false,
    bool DashboardOnly = false,
    bool OpenBrowser = false,
    int? ApiPort = null,
    string? Environment = null
);
