using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using SourceWorker.Contracts;

namespace SourceWorker.AppHost;

public sealed class ProcessLauncher(ILogger<ProcessLauncher> logger) : IProcessLauncher
{
    private readonly List<Process> _managedProcesses = new();

    public async Task<ManagedProcessInfo> StartProcessAsync(string name, string command, string arguments, IDictionary<string, string>? environmentVariables = null)
    {
        logger.LogInformation("Starting process {Name}: {Command} {Arguments}", name, command, arguments);

        var startInfo = new ProcessStartInfo
        {
            FileName = command,
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = false, // Keep it visible for now or use true if requested
            RedirectStandardOutput = false,
            RedirectStandardError = false
        };

        if (environmentVariables != null)
        {
            foreach (var (key, value) in environmentVariables)
            {
                startInfo.EnvironmentVariables[key] = value;
            }
        }

        var process = new Process { StartInfo = startInfo };
        process.Start();
        _managedProcesses.Add(process);

        return new ManagedProcessInfo(
            name,
            process.Id,
            ManagedProcessStatus.Running,
            null,
            DateTimeOffset.UtcNow
        );
    }

    public Task StopProcessAsync(int processId)
    {
        var process = _managedProcesses.FirstOrDefault(p => p.Id == processId);
        if (process != null && !process.HasExited)
        {
            logger.LogInformation("Stopping process {Pid}", processId);
            process.Kill(true);
        }
        return Task.CompletedTask;
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
