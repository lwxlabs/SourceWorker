using SourceWorker.Contracts;

namespace SourceWorker.Hosting;

public interface IProcessLauncher
{
    Task<ManagedProcessInfo> StartProcessAsync(string name, string command, string arguments, IDictionary<string, string>? environmentVariables = null);
    Task StopProcessAsync(int processId);
    Task StopAllAsync();
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

public interface ILocalApiLifecycleService
{
    Task<LocalApiLifecycleResult> StartAsync(LocalApiLaunchOptions options, CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
    LocalApiLifecycleResult? Current { get; }
}

public interface IRuntimePathResolver
{
    string FindSolutionRoot();
    string GetApiTokenPath();
    string? ReadApiToken();
}
