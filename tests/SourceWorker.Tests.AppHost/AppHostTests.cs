using Microsoft.Extensions.Logging;
using Moq;
using SourceWorker.Contracts;
using SourceWorker.Hosting;
using Xunit;

namespace SourceWorker.Tests.AppHost;

public sealed class AppHostTests
{
    private readonly Mock<ILocalApiLifecycleService> localApi = new();
    private readonly Mock<IRuntimePathResolver> paths = new();
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
        orchestrator = new AppHostOrchestrator(
            localApi.Object,
            paths.Object,
            Mock.Of<ILogger<AppHostOrchestrator>>());
    }

    [Fact]
    public async Task StartAsync_StartsApiOnlyByDefault()
    {
        await orchestrator.StartAsync(new AppHostOptions());

        localApi.Verify(p => p.StartAsync(It.IsAny<LocalApiLaunchOptions>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StartAsync_WithOpenBrowser_CallsBrowserLauncher()
    {
        await orchestrator.StartAsync(new AppHostOptions(OpenBrowser: true));

        localApi.Verify(p => p.StartAsync(It.IsAny<LocalApiLaunchOptions>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StartAsync_WithApiOnly_StartsApiOnly()
    {
        await orchestrator.StartAsync(new AppHostOptions(ApiOnly: true));

        localApi.Verify(p => p.StartAsync(It.IsAny<LocalApiLaunchOptions>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StopAsync_StopsOwnedApi()
    {
        await orchestrator.StartAsync(new AppHostOptions());

        await orchestrator.StopAsync();

        localApi.Verify(p => p.StopAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
