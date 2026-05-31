using Microsoft.Extensions.Logging;
using Moq;
using SourceWorker.Contracts;
using SourceWorker.Hosting;
using Xunit;

namespace SourceWorker.Tests.AppHost;

public sealed class AppHostTests
{
    private readonly Mock<ILocalApiLifecycleService> localApi = new();
    private readonly Mock<IProcessLauncher> processLauncher = new();
    private readonly Mock<IRuntimePathResolver> paths = new();
    private readonly Mock<ILocalBrowserLauncher> browser = new();
    private readonly AppHostOrchestrator orchestrator;

    public AppHostTests()
    {
        paths.Setup(p => p.FindSolutionRoot()).Returns(Path.GetTempPath());
        localApi.Setup(a => a.StartAsync(It.IsAny<LocalApiLaunchOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LocalApiLifecycleResult(
                new ManagedProcessInfo("LocalApi", 111, ManagedProcessStatus.Running, "http://localhost:5000", DateTimeOffset.UtcNow),
                true,
                false,
                "http://localhost:5000",
                "token",
                DateTimeOffset.UtcNow,
                TimeSpan.FromMilliseconds(10),
                ["starting Local API"]));
        processLauncher.Setup(p => p.StartProcessAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IDictionary<string, string>>()))
            .ReturnsAsync((string name, string _, string _, IDictionary<string, string> _) => new ManagedProcessInfo(name, 222, ManagedProcessStatus.Running, null, DateTimeOffset.UtcNow));

        orchestrator = new AppHostOrchestrator(
            localApi.Object,
            processLauncher.Object,
            paths.Object,
            browser.Object,
            Mock.Of<ILogger<AppHostOrchestrator>>());
    }

    [Fact]
    public async Task StartAsync_StartsApiAndDashboardByDefault()
    {
        await orchestrator.StartAsync(new AppHostOptions());

        localApi.Verify(p => p.StartAsync(It.IsAny<LocalApiLaunchOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        processLauncher.Verify(p => p.StartProcessAsync("Dashboard", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IDictionary<string, string>>()), Times.Once);
        browser.Verify(p => p.OpenBrowser(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task StartAsync_WithOpenBrowser_CallsBrowserLauncher()
    {
        await orchestrator.StartAsync(new AppHostOptions(OpenBrowser: true));

        browser.Verify(p => p.OpenBrowser(It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task StartAsync_WithApiOnly_DoesNotStartDashboard()
    {
        await orchestrator.StartAsync(new AppHostOptions(ApiOnly: true));

        localApi.Verify(p => p.StartAsync(It.IsAny<LocalApiLaunchOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        processLauncher.Verify(p => p.StartProcessAsync("Dashboard", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IDictionary<string, string>>()), Times.Never);
    }

    [Fact]
    public async Task StartAsync_WithDashboardOnly_DoesNotStartApi()
    {
        await orchestrator.StartAsync(new AppHostOptions(DashboardOnly: true));

        localApi.Verify(p => p.StartAsync(It.IsAny<LocalApiLaunchOptions>(), It.IsAny<CancellationToken>()), Times.Never);
        processLauncher.Verify(p => p.StartProcessAsync("Dashboard", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IDictionary<string, string>>()), Times.Once);
    }

    [Fact]
    public async Task StopAsync_StopsDashboardAndOwnedApi()
    {
        await orchestrator.StartAsync(new AppHostOptions());

        await orchestrator.StopAsync();

        processLauncher.Verify(p => p.StopProcessAsync(222), Times.Once);
        localApi.Verify(p => p.StopAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
