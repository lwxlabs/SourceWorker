using SourceWorker.Contracts;

namespace SourceWorker.Hosting;

public sealed record AppHostOptions(
    bool ApiOnly = false,
    bool OpenBrowser = false,
    int? ApiPort = null,
    string? Environment = null
);

public enum LaunchMode
{
    AppHost,
    Desktop
}

public sealed record LocalApiLaunchOptions(
    int? ApiPort = null,
    string? SolutionRoot = null,
    bool ReuseExisting = true,
    LaunchMode LaunchMode = LaunchMode.AppHost
);

public sealed record LocalApiLifecycleResult(
    ManagedProcessInfo Process,
    bool StartedByOwner,
    bool ReusedExisting,
    string BaseUrl,
    string? Token,
    DateTimeOffset StartedUtc,
    TimeSpan StartupDuration,
    IReadOnlyList<string> StartupSteps,
    string? ErrorMessage = null
);

public sealed record DesktopRuntimeState(
    bool DesktopMode,
    bool LocalApiManagedByDesktop,
    int? LocalApiProcessId,
    string? LocalApiBaseUrl,
    bool TokenConfigured,
    string? StoragePath,
    string AppVersion,
    string UiVersion,
    string? ApiVersion,
    TimeSpan StartupDuration,
    string? LastStartupError,
    bool OwnedLocalApiProcess,
    IReadOnlyList<string> StartupSteps
);
