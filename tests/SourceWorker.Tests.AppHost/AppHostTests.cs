using Moq;
using Xunit;
using SourceWorker.AppHost;
using SourceWorker.Contracts;
using Microsoft.Extensions.Logging;

namespace SourceWorker.Tests.AppHost;

public class AppHostTests
{
    private readonly Mock<IProcessLauncher> _processLauncherMock = new();
    private readonly Mock<IEndpointProbe> _endpointProbeMock = new();
    private readonly Mock<ILocalBrowserLauncher> _browserLauncherMock = new();
    private readonly Mock<ILogger<AppHostOrchestrator>> _loggerMock = new();
    private readonly AppHostOrchestrator _orchestrator;

    public AppHostTests()
    {
        _orchestrator = new AppHostOrchestrator(
            _processLauncherMock.Object,
            _endpointProbeMock.Object,
            _browserLauncherMock.Object,
            _loggerMock.Object
        );

        // Default healthy probe result
        _endpointProbeMock
            .Setup(p => p.ProbeEndpointAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LocalEndpointProbeResult(true, "OK", 200));

        // Default process start result
        _processLauncherMock
            .Setup(p => p.StartProcessAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IDictionary<string, string>>()))
            .ReturnsAsync((string name, string cmd, string args, IDictionary<string, string> env) => 
                new ManagedProcessInfo(name, 123, ManagedProcessStatus.Running, null, DateTimeOffset.UtcNow));
    }

    [Fact]
    public async Task StartAsync_StartsApiAndDashboardByDefault()
    {
        // Arrange
        var options = new AppHostOptions();

        // Act
        await _orchestrator.StartAsync(options);

        // Assert
        _processLauncherMock.Verify(p => p.StartProcessAsync("LocalApi", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IDictionary<string, string>>()), Times.Once);
        _processLauncherMock.Verify(p => p.StartProcessAsync("Dashboard", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IDictionary<string, string>>()), Times.Once);
        _browserLauncherMock.Verify(p => p.OpenBrowser(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task StartAsync_WithOpenBrowser_CallsBrowserLauncher()
    {
        // Arrange
        var options = new AppHostOptions(OpenBrowser: true);

        // Act
        await _orchestrator.StartAsync(options);

        // Assert
        _browserLauncherMock.Verify(p => p.OpenBrowser(It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task StartAsync_WithApiOnly_DoesNotStartDashboard()
    {
        // Arrange
        var options = new AppHostOptions(ApiOnly: true);

        // Act
        await _orchestrator.StartAsync(options);

        // Assert
        _processLauncherMock.Verify(p => p.StartProcessAsync("LocalApi", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IDictionary<string, string>>()), Times.Once);
        _processLauncherMock.Verify(p => p.StartProcessAsync("Dashboard", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IDictionary<string, string>>()), Times.Never);
    }

    [Fact]
    public async Task StartAsync_WithDashboardOnly_DoesNotStartApi()
    {
        // Arrange
        var options = new AppHostOptions(DashboardOnly: true);

        // Act
        await _orchestrator.StartAsync(options);

        // Assert
        _processLauncherMock.Verify(p => p.StartProcessAsync("LocalApi", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IDictionary<string, string>>()), Times.Never);
        _processLauncherMock.Verify(p => p.StartProcessAsync("Dashboard", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IDictionary<string, string>>()), Times.Once);
    }

    [Fact]
    public async Task StopAsync_StopsManagedProcesses()
    {
        // Arrange
        var options = new AppHostOptions();
        await _orchestrator.StartAsync(options);

        // Act
        await _orchestrator.StopAsync();

        // Assert
        _processLauncherMock.Verify(p => p.StopProcessAsync(It.IsAny<int>()), Times.Exactly(2));
    }
}
