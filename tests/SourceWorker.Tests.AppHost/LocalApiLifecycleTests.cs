using Microsoft.Extensions.Logging;
using Moq;
using SourceWorker.Contracts;
using SourceWorker.Hosting;
using Xunit;

namespace SourceWorker.Tests.AppHost;

public sealed class LocalApiLifecycleTests
{
    [Fact]
    public async Task StartAsync_reuses_existing_api_when_healthy()
    {
        var launcher = new Mock<IProcessLauncher>();
        var probe = new Mock<IEndpointProbe>();
        var paths = new Mock<IRuntimePathResolver>();
        paths.Setup(p => p.ReadApiToken()).Returns("token");
        probe.Setup(p => p.ProbeEndpointAsync("http://localhost:5000/health", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LocalEndpointProbeResult(true, "OK", 200));

        var service = new LocalApiLifecycleService(launcher.Object, probe.Object, paths.Object, Mock.Of<ILogger<LocalApiLifecycleService>>());

        var result = await service.StartAsync(new LocalApiLaunchOptions(ReuseExisting: true));

        Assert.True(result.ReusedExisting);
        Assert.False(result.StartedByOwner);
        Assert.Equal("http://localhost:5000", result.BaseUrl);
        Assert.Equal("token", result.Token);
        launcher.Verify(l => l.StartProcessAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IDictionary<string, string>>()), Times.Never);
    }

    [Fact]
    public async Task StartAsync_starts_api_when_existing_probe_fails()
    {
        var launcher = new Mock<IProcessLauncher>();
        var probe = new Mock<IEndpointProbe>();
        var paths = new Mock<IRuntimePathResolver>();
        paths.Setup(p => p.FindSolutionRoot()).Returns(Path.GetTempPath());
        paths.Setup(p => p.ReadApiToken()).Returns("token");
        probe.SetupSequence(p => p.ProbeEndpointAsync("http://localhost:5000/health", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LocalEndpointProbeResult(false, "offline", null))
            .ReturnsAsync(new LocalEndpointProbeResult(true, "OK", 200));
        launcher.Setup(l => l.StartProcessAsync("LocalApi", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IDictionary<string, string>>()))
            .ReturnsAsync(new ManagedProcessInfo("LocalApi", 123, ManagedProcessStatus.Running, null, DateTimeOffset.UtcNow));

        var service = new LocalApiLifecycleService(launcher.Object, probe.Object, paths.Object, Mock.Of<ILogger<LocalApiLifecycleService>>());

        var result = await service.StartAsync(new LocalApiLaunchOptions(ReuseExisting: true));

        Assert.True(result.StartedByOwner);
        Assert.False(result.ReusedExisting);
        Assert.Equal(123, result.Process.ProcessId);
    }

    [Fact]
    public async Task StopAsync_stops_only_owned_api()
    {
        var launcher = new Mock<IProcessLauncher>();
        var probe = new Mock<IEndpointProbe>();
        var paths = new Mock<IRuntimePathResolver>();
        paths.Setup(p => p.FindSolutionRoot()).Returns(Path.GetTempPath());
        probe.SetupSequence(p => p.ProbeEndpointAsync("http://localhost:5000/health", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LocalEndpointProbeResult(false, "offline", null))
            .ReturnsAsync(new LocalEndpointProbeResult(true, "OK", 200));
        launcher.Setup(l => l.StartProcessAsync("LocalApi", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IDictionary<string, string>>()))
            .ReturnsAsync(new ManagedProcessInfo("LocalApi", 456, ManagedProcessStatus.Running, null, DateTimeOffset.UtcNow));

        var service = new LocalApiLifecycleService(launcher.Object, probe.Object, paths.Object, Mock.Of<ILogger<LocalApiLifecycleService>>());
        await service.StartAsync(new LocalApiLaunchOptions());

        await service.StopAsync();

        launcher.Verify(l => l.StopProcessAsync(456), Times.Once);
    }
}
