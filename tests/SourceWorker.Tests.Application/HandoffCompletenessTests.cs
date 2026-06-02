using SourceWorker.Application;
using SourceWorker.Context;
using SourceWorker.Domain;

namespace SourceWorker.Tests.Application;

public sealed class HandoffCompletenessTests
{
    [Fact]
    public async Task Completeness_warns_when_csharp_files_exist_but_no_symbols_are_persisted()
    {
        var project = new Project(Guid.NewGuid(), "Sample", "C:\\Sample", DateTimeOffset.UtcNow);
        var scanRun = new ScanRun(Guid.NewGuid(), project.Id, DateTimeOffset.UtcNow.AddSeconds(-1), DateTimeOffset.UtcNow, ScanRunStatus.Completed, 1, 0);
        var files = new[]
        {
            new IndexedFile(Guid.NewGuid(), project.Id, scanRun.Id, "C:\\Sample\\Program.cs", "Program.cs", ".cs", 10, DateTimeOffset.UtcNow, "hash", IndexedFileKind.Source, false, null, FileChangeKind.New)
        };
        var service = new ScanCompletenessService(
            new ScanRepo(scanRun),
            new IndexedRepo(files),
            new ArtifactRepo([]),
            new SymbolRepo([]),
            new FlowRepo([]),
            new RouteRepo([]),
            new ServiceRepo([]),
            new ConfigRepo([]),
            new TestRepo([]));

        var report = await service.GetAsync(project, CancellationToken.None);

        Assert.Contains(report.Stages, s => s.Name == "C# symbol scan" && s.Status == ScanStageStatus.CompletedWithWarnings);
        Assert.Contains(report.Warnings, w => w.Message.Contains("no symbols", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("C# files were found", report.CodeIntelligenceExplanation);
    }

    private sealed class ScanRepo(ScanRun run) : IScanRunRepository
    {
        public Task<ScanRun> AddAsync(ScanRun scanRun, CancellationToken cancellationToken) => Task.FromResult(scanRun);
        public Task<ScanRun?> GetAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult<ScanRun?>(run.Id == id ? run : null);
        public Task<IReadOnlyList<ScanRun>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ScanRun>>([run]);
        public Task<ScanRun?> GetLatestAsync(Guid projectId, CancellationToken cancellationToken) => Task.FromResult<ScanRun?>(run);
        public Task<ScanRun> UpdateAsync(ScanRun scanRun, CancellationToken cancellationToken) => Task.FromResult(scanRun);
    }

    private sealed class IndexedRepo(IReadOnlyList<IndexedFile> files) : IIndexedFileRepository
    {
        public Task SaveForScanAsync(Guid projectId, Guid scanRunId, IReadOnlyList<IndexedFile> files, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<IReadOnlyList<IndexedFile>> GetByScanRunAsync(Guid scanRunId, CancellationToken cancellationToken) => Task.FromResult(files);
        public Task<IReadOnlyList<IndexedFile>> GetLatestByProjectAsync(Guid projectId, CancellationToken cancellationToken) => Task.FromResult(files);
    }

    private sealed class ArtifactRepo(IReadOnlyList<GeneratedArtifact> artifacts) : IGeneratedArtifactRepository
    {
        public Task<GeneratedArtifact> UpsertAsync(GeneratedArtifact artifact, CancellationToken cancellationToken) => Task.FromResult(artifact);
        public Task<IReadOnlyList<GeneratedArtifact>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken) => Task.FromResult(artifacts);
        public Task<GeneratedArtifact?> GetByPathAsync(Guid projectId, string relativePath, CancellationToken cancellationToken) => Task.FromResult<GeneratedArtifact?>(null);
    }

    private sealed class SymbolRepo(IReadOnlyList<CodeSymbol> items) : ICodeSymbolRepository
    {
        public Task SaveAsync(Guid projectId, Guid scanRunId, IReadOnlyList<CodeSymbol> symbols, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<IReadOnlyList<CodeSymbol>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken) => Task.FromResult(items);
    }

    private sealed class FlowRepo(IReadOnlyList<CodeFlow> items) : ICodeFlowRepository
    {
        public Task SaveAsync(Guid projectId, Guid scanRunId, IReadOnlyList<CodeFlow> flows, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<IReadOnlyList<CodeFlow>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken) => Task.FromResult(items);
    }

    private sealed class RouteRepo(IReadOnlyList<RouteInfo> items) : IRouteInfoRepository
    {
        public Task SaveAsync(Guid projectId, Guid scanRunId, IReadOnlyList<RouteInfo> routes, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<IReadOnlyList<RouteInfo>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken) => Task.FromResult(items);
    }

    private sealed class ServiceRepo(IReadOnlyList<ServiceRegistration> items) : IServiceRegistrationRepository
    {
        public Task SaveAsync(Guid projectId, Guid scanRunId, IReadOnlyList<ServiceRegistration> registrations, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<IReadOnlyList<ServiceRegistration>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken) => Task.FromResult(items);
    }

    private sealed class ConfigRepo(IReadOnlyList<ConfigurationUsage> items) : IConfigurationUsageRepository
    {
        public Task SaveAsync(Guid projectId, Guid scanRunId, IReadOnlyList<ConfigurationUsage> usages, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<IReadOnlyList<ConfigurationUsage>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken) => Task.FromResult(items);
    }

    private sealed class TestRepo(IReadOnlyList<TestInfo> items) : ITestInfoRepository
    {
        public Task SaveAsync(Guid projectId, Guid scanRunId, IReadOnlyList<TestInfo> tests, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<IReadOnlyList<TestInfo>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken) => Task.FromResult(items);
    }
}

