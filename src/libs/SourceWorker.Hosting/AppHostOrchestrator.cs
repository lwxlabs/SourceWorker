using Microsoft.Extensions.Logging;
using SourceWorker.Contracts;

namespace SourceWorker.Hosting;

public sealed class AppHostOrchestrator(
    ILocalApiLifecycleService localApi,
    IRuntimePathResolver paths,
    ILogger<AppHostOrchestrator> logger) : IAppHostOrchestrator
{
    private DateTimeOffset startedAt;

    public async Task StartAsync(AppHostOptions options, CancellationToken cancellationToken = default)
    {
        startedAt = DateTimeOffset.UtcNow;
        var solutionRoot = paths.FindSolutionRoot();
        logger.LogInformation("Solution root found at: {Path}", solutionRoot);

        await localApi.StartAsync(new LocalApiLaunchOptions(options.ApiPort, solutionRoot, ReuseExisting: false, LaunchMode.AppHost), cancellationToken);

        if (options.OpenBrowser)
        {
            logger.LogInformation("The web dashboard host has been removed. Use SourceWorker.Desktop for the UI.");
        }
    }

    public async Task StopAsync()
    {
        await localApi.StopAsync();
    }

    public AppHostRuntimeState GetState()
        => new(
            localApi.Current?.Process ?? new ManagedProcessInfo("LocalApi", null, ManagedProcessStatus.NotStarted, null, null),
            null,
            startedAt,
            "1.0.0");
}
