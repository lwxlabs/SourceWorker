using System.Threading;
using SourceWorker.ApiClient;
using SourceWorker.Hosting;
using SourceWorker.UI.Services;

namespace SourceWorker.Desktop.Services;

public interface IDesktopLocalApiLifecycleService
{
    Task<DesktopStartupState> StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
    DesktopRuntimeState GetRuntimeState();
}

public interface IDesktopStartupService
{
    Task<DesktopStartupState> StartAsync(CancellationToken cancellationToken = default);
}

public interface IDesktopRuntimeStateService
{
    DesktopRuntimeState GetState();
}

public interface IDesktopApiConnectionService
{
    Task<ApiConnectionStatus> CheckAsync(CancellationToken cancellationToken = default);
}

public interface IDesktopStartupDiagnosticsService
{
    string BuildDiagnostics(DesktopStartupState state, DesktopRuntimeState runtime);
}

public interface IDesktopShellService { string EnvironmentName { get; } }
public interface IDesktopWindowService { void ShowMainWindow(); }
public interface IDesktopNotificationService { Task NotifyAsync(string title, string message, CancellationToken cancellationToken = default); }
public interface IDesktopClipboardService { Task SetTextAsync(string text, CancellationToken cancellationToken = default); }
public interface IDesktopFileDialogService { Task<string?> PickFolderAsync(CancellationToken cancellationToken = default); }
public interface IDesktopExternalLinkService { Task OpenAsync(string url, CancellationToken cancellationToken = default); }
public interface ISingleInstanceService { bool TryAcquire(); void Release(); }

public sealed class DesktopLocalApiLifecycleService(
    ILocalApiLifecycleService localApi,
    SourceWorkerApiClientOptions options) : IDesktopLocalApiLifecycleService, IDesktopRuntimeStateService
{
    private DesktopStartupState lastStartup = DesktopStartupState.StartingShell();
    private LocalApiLifecycleResult? result;

    public async Task<DesktopStartupState> StartAsync(CancellationToken cancellationToken = default)
    {
        var started = DateTimeOffset.UtcNow;
        try
        {
            lastStartup = new DesktopStartupState(DesktopStartupStatus.Starting, "starting Local API", ["starting desktop shell", "starting Local API"], null, null, TimeSpan.Zero);
            result = await localApi.StartAsync(new LocalApiLaunchOptions(ReuseExisting: true, LaunchMode: LaunchMode.Desktop), cancellationToken);
            options.BaseUrl = result.BaseUrl;
            options.BearerToken = result.Token;
            options.LaunchMode = "Desktop";
            var steps = result.StartupSteps.Concat(["loading dashboard UI", "ready"]).ToArray();
            lastStartup = new DesktopStartupState(DesktopStartupStatus.Ready, "ready", steps, null, null, DateTimeOffset.UtcNow - started);
            return lastStartup;
        }
        catch (Exception ex)
        {
            lastStartup = new DesktopStartupState(
                DesktopStartupStatus.Failed,
                "failed",
                lastStartup.Steps.Append("failed").ToArray(),
                ex.Message,
                "Retry startup, check whether port 5000 is already in use, or run `dotnet run --project src/services/SourceWorker.LocalApi` for details.",
                DateTimeOffset.UtcNow - started);
            return lastStartup;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken = default) => localApi.StopAsync(cancellationToken);

    public DesktopRuntimeState GetRuntimeState() => GetState();

    public DesktopRuntimeState GetState()
        => new(
            DesktopMode: true,
            LocalApiManagedByDesktop: result?.StartedByOwner == true,
            LocalApiProcessId: result?.Process.ProcessId,
            LocalApiBaseUrl: result?.BaseUrl ?? options.BaseUrl,
            TokenConfigured: !string.IsNullOrWhiteSpace(options.BearerToken),
            StoragePath: null,
            AppVersion: "2.4.0",
            UiVersion: "2.4.0",
            ApiVersion: null,
            StartupDuration: lastStartup.Duration,
            LastStartupError: lastStartup.ErrorMessage,
            OwnedLocalApiProcess: result?.StartedByOwner == true,
            StartupSteps: lastStartup.Steps);
}

public sealed class DesktopStartupService(IDesktopLocalApiLifecycleService lifecycle) : IDesktopStartupService
{
    public Task<DesktopStartupState> StartAsync(CancellationToken cancellationToken = default) => lifecycle.StartAsync(cancellationToken);
}

public sealed class DesktopApiConnectionService(ISourceWorkerApiClient api) : IDesktopApiConnectionService
{
    public Task<ApiConnectionStatus> CheckAsync(CancellationToken cancellationToken = default) => api.CheckConnectionAsync(cancellationToken);
}

public sealed class DesktopStartupDiagnosticsService : IDesktopStartupDiagnosticsService
{
    public string BuildDiagnostics(DesktopStartupState state, DesktopRuntimeState runtime)
        => $"""
        SourceWorker Desktop Diagnostics
        Status: {state.Status}
        Current step: {state.CurrentStep}
        Local API URL: {runtime.LocalApiBaseUrl}
        Token configured: {runtime.TokenConfigured}
        Owned Local API: {runtime.OwnedLocalApiProcess}
        Startup duration: {state.Duration}
        Error: {state.ErrorMessage ?? "none"}
        """;
}

public sealed class DesktopShellService : IDesktopShellService { public string EnvironmentName => "Desktop"; }
public sealed class DesktopWindowService : IDesktopWindowService { public void ShowMainWindow() { } }
public sealed class InAppDesktopNotificationService : IDesktopNotificationService { public Task NotifyAsync(string title, string message, CancellationToken cancellationToken = default) => Task.CompletedTask; }
public sealed class DesktopClipboardService : IDesktopClipboardService { public Task SetTextAsync(string text, CancellationToken cancellationToken = default) => Clipboard.SetTextAsync(text); }
public sealed class DesktopExternalLinkService : IDesktopExternalLinkService { public Task OpenAsync(string url, CancellationToken cancellationToken = default) => Launcher.OpenAsync(url); }

public sealed class DesktopFolderPickerService : IDesktopFileDialogService, IProjectFolderPicker
{
    public Task<string?> PickFolderAsync(CancellationToken cancellationToken = default)
        => PickAsync(cancellationToken);

    async Task<string?> IDesktopFileDialogService.PickFolderAsync(CancellationToken cancellationToken)
        => await PickAsync(cancellationToken);

    async Task<string?> IProjectFolderPicker.PickFolderAsync(CancellationToken cancellationToken)
        => await PickAsync(cancellationToken);

    private static async Task<string?> PickAsync(CancellationToken cancellationToken)
    {
        var result = await FilePicker.PickAsync(new PickOptions { PickerTitle = "Choose a project folder" });
        return result?.FullPath is null ? null : Directory.Exists(result.FullPath) ? result.FullPath : Path.GetDirectoryName(result.FullPath);
    }
}

public sealed class SingleInstanceService : ISingleInstanceService, IDisposable
{
    private Mutex? mutex;

    public bool TryAcquire()
    {
        mutex = new Mutex(initiallyOwned: true, "SourceWorker.Desktop.SingleInstance", out var created);
        return created;
    }

    public void Release()
    {
        try { mutex?.ReleaseMutex(); } catch { }
        mutex?.Dispose();
        mutex = null;
    }

    public void Dispose() => Release();
}
