using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SourceWorker.AppHost;

var serviceCollection = new ServiceCollection();

serviceCollection.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));
serviceCollection.AddHttpClient();
serviceCollection.AddSingleton<IProcessLauncher, ProcessLauncher>();
serviceCollection.AddSingleton<IEndpointProbe, EndpointProbe>();
serviceCollection.AddSingleton<ILocalBrowserLauncher, LocalBrowserLauncher>();
serviceCollection.AddSingleton<IAppHostOrchestrator, AppHostOrchestrator>();

var serviceProvider = serviceCollection.BuildServiceProvider();
var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
var orchestrator = serviceProvider.GetRequiredService<IAppHostOrchestrator>();

var options = ParseOptions(args);

var cts = new CancellationTokenSource();
Console.CancelKeyPress += (s, e) =>
{
    e.Cancel = true;
    logger.LogInformation("Shutdown requested...");
    cts.Cancel();
};

try
{
    logger.LogInformation("SourceWorker AppHost starting...");
    await orchestrator.StartAsync(options, cts.Token);
    
    var state = orchestrator.GetState();
    logger.LogInformation("SourceWorker AppHost is running.");
    logger.LogInformation("Local API: {Url} (PID: {Pid})", state.LocalApi.BaseUrl, state.LocalApi.ProcessId);
    if (state.Dashboard != null)
    {
        logger.LogInformation("Dashboard: {Url} (PID: {Pid})", state.Dashboard.BaseUrl, state.Dashboard.ProcessId);
    }
    
    logger.LogInformation("Press Ctrl+C to shut down.");
    
    while (!cts.Token.IsCancellationRequested)
    {
        await Task.Delay(1000, cts.Token);
    }
}
catch (OperationCanceledException)
{
    // Normal shutdown
}
catch (Exception ex)
{
    logger.LogError(ex, "An error occurred during AppHost execution.");
}
finally
{
    logger.LogInformation("Shutting down managed processes...");
    await orchestrator.StopAsync();
    logger.LogInformation("AppHost shut down.");
}

AppHostOptions ParseOptions(string[] args)
{
    var apiOnly = args.Contains("--api-only");
    var dashboardOnly = args.Contains("--dashboard-only");
    var open = args.Contains("--open");
    var noOpen = args.Contains("--no-open");
    
    int? port = null;
    var portArg = args.FirstOrDefault(a => a.StartsWith("--port="));
    if (portArg != null && int.TryParse(portArg["--port=".Length..], out var p))
    {
        port = p;
    }

    return new AppHostOptions
    {
        ApiOnly = apiOnly,
        DashboardOnly = dashboardOnly,
        OpenBrowser = open && !noOpen,
        ApiPort = port
    };
}
