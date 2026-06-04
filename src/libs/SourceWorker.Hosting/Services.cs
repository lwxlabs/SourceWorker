using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SourceWorker.Contracts;

namespace SourceWorker.Hosting;

public static class DependencyInjection
{
    public static IServiceCollection AddSourceWorkerHosting(this IServiceCollection services)
    {
        services.AddHttpClient();
        services.AddSingleton<IRuntimePathResolver, RuntimePathResolver>();
        services.AddSingleton<IProcessLauncher, ProcessLauncher>();
        services.AddSingleton<IEndpointProbe, EndpointProbe>();
        services.AddSingleton<ILocalBrowserLauncher, LocalBrowserLauncher>();
        services.AddSingleton<ILocalApiLifecycleService, LocalApiLifecycleService>();
        services.AddSingleton<IAppHostOrchestrator, AppHostOrchestrator>();
        return services;
    }
}

public sealed class RuntimePathResolver : IRuntimePathResolver
{
    public string FindSolutionRoot()
    {
        var current = Directory.GetCurrentDirectory();
        while (!string.IsNullOrWhiteSpace(current))
        {
            if (File.Exists(Path.Combine(current, "SourceWorker.slnx")) || File.Exists(Path.Combine(current, "SourceWorker.sln")))
            {
                return current;
            }

            current = Path.GetDirectoryName(current);
        }

        return Directory.GetCurrentDirectory();
    }

    public string GetApiTokenPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(appData, "SourceWorker", "api-token.txt");
    }

    public string? ReadApiToken()
    {
        var path = GetApiTokenPath();
        return File.Exists(path) ? File.ReadAllText(path).Trim() : null;
    }
}

public sealed class ProcessLauncher(ILogger<ProcessLauncher> logger) : IProcessLauncher
{
    private readonly List<Process> managedProcesses = [];

    public Task<ManagedProcessInfo> StartProcessAsync(string name, string command, string arguments, IDictionary<string, string>? environmentVariables = null)
    {
        logger.LogInformation("Starting process {Name}: {Command} {Arguments}", name, command, arguments);
        var startInfo = new ProcessStartInfo
        {
            FileName = command,
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = false,
            RedirectStandardError = false
        };

        if (environmentVariables is not null)
        {
            foreach (var (key, value) in environmentVariables)
            {
                startInfo.EnvironmentVariables[key] = value;
            }
        }

        var process = new Process { StartInfo = startInfo };
        process.Start();
        managedProcesses.Add(process);

        return Task.FromResult(new ManagedProcessInfo(name, process.Id, ManagedProcessStatus.Running, null, DateTimeOffset.UtcNow));
    }

    public Task StopProcessAsync(int processId)
    {
        var process = managedProcesses.FirstOrDefault(p => p.Id == processId);
        try
        {
            process ??= Process.GetProcessById(processId);
            if (!process.HasExited)
            {
                logger.LogInformation("Stopping process {Pid}", processId);
                process.Kill(entireProcessTree: true);
                process.WaitForExit(5000);
            }
        }
        catch (ArgumentException)
        {
            logger.LogDebug("Process {Pid} was already stopped", processId);
        }
        catch (InvalidOperationException)
        {
            logger.LogDebug("Process {Pid} was already stopped", processId);
        }

        return Task.CompletedTask;
    }

    public async Task StopAllAsync()
    {
        foreach (var process in managedProcesses.ToArray())
        {
            if (process.HasExited) continue;
            await StopProcessAsync(process.Id);
        }

        managedProcesses.RemoveAll(process =>
        {
            try { return process.HasExited; }
            catch { return true; }
        });
    }

    public bool IsProcessRunning(int processId)
    {
        try
        {
            var process = Process.GetProcessById(processId);
            return !process.HasExited;
        }
        catch
        {
            return false;
        }
    }
}

public sealed class EndpointProbe(IHttpClientFactory httpClientFactory, ILogger<EndpointProbe> logger) : IEndpointProbe
{
    public async Task<LocalEndpointProbeResult> ProbeEndpointAsync(string url, CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(2);
            var response = await client.GetAsync(url, cancellationToken);
            return new LocalEndpointProbeResult(response.IsSuccessStatusCode, response.ReasonPhrase, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            logger.LogTrace("Probe failed for {Url}: {Message}", url, ex.Message);
            return new LocalEndpointProbeResult(false, ex.Message, null);
        }
    }
}

public sealed class LocalBrowserLauncher : ILocalBrowserLauncher
{
    public void OpenBrowser(string url)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            Process.Start("xdg-open", url);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            Process.Start("open", url);
        }
    }
}
