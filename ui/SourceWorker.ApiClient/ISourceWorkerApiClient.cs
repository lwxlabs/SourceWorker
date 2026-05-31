namespace SourceWorker.ApiClient;

public interface ISourceWorkerApiClient
{
    Task<HealthResponse?> GetHealthAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProjectDto>> GetProjectsAsync(CancellationToken cancellationToken = default);
    Task<DoctorReport> GetDoctorAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<ContextFreshnessReport> GetContextStatusAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<StorageInfo> GetStorageInfoAsync(CancellationToken cancellationToken = default);
    Task<ArtifactLifecycleReport> GetArtifactsAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<CleanupPreviewReport> GetCleanupPreviewAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CodeSymbol>> GetSymbolsAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CodeFlow>> GetFlowsAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RouteInfo>> GetRoutesAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ServiceRegistration>> GetServiceRegistrationsAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ConfigurationUsage>> GetConfigurationUsagesAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TestInfo>> GetTestsAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<ContextBrowserSummary> GetContextBrowserAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ContextBrowserFile>> GetContextFilesAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<string> ReadContextAsync(Guid projectId, string relativePath, CancellationToken cancellationToken = default);
    Task<GeneratedOutputSet> GetOutputsAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ContextPackManifest>> GetPacksAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<ContextPackManifest?> GetPackAsync(Guid projectId, string packId, CancellationToken cancellationToken = default);
    Task<string> GetPackContentAsync(Guid projectId, string packId, CancellationToken cancellationToken = default);
    Task<ExportRun> RunExportAsync(Guid projectId, string exportProfileId, CancellationToken cancellationToken = default);
}
