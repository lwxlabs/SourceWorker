namespace SourceWorker.ApiClient;

public interface ISourceWorkerApiClient
{
    Task<HealthResponse?> GetHealthAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProjectDto>> GetProjectsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProjectSummaryDto>> GetProjectsSummaryAsync(bool includeTestProjects = false, bool includeMissing = false, string? search = null, int? limit = null, CancellationToken cancellationToken = default);
    Task<ProjectDto> RegisterProjectAsync(string path, string? name = null, CancellationToken cancellationToken = default);
    Task<StartScanResponse> StartProjectScanAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<ApiRuntimeInfoDto> GetRuntimeInfoAsync(CancellationToken cancellationToken = default);
    Task<ApiSecurityInfoDto> GetSecurityInfoAsync(CancellationToken cancellationToken = default);
    Task<string> RotateTokenAsync(CancellationToken cancellationToken = default);
    Task<VersionInfoDto> GetVersionAsync(CancellationToken cancellationToken = default);
    Task<ApiConnectionStatus> CheckConnectionAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<JobStatusDto>> GetJobsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<JobStatusDto>> GetProjectJobsAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<JobStatusDto?> GetJobStatusAsync(Guid jobId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetJobLogsAsync(Guid jobId, CancellationToken cancellationToken = default);

    Task<DoctorReport> GetDoctorAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<ContextFreshnessReport> GetContextStatusAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<StorageInfo> GetStorageInfoAsync(CancellationToken cancellationToken = default);
    Task<ArtifactLifecycleReport> GetArtifactsAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<CleanupPreviewDto> GetCleanupPreviewAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<CleanupGeneratedResponse> CleanupGeneratedAsync(Guid projectId, bool dryRun = false, bool force = false, CancellationToken cancellationToken = default);
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
    Task<ExportPackResponse> ExportPackToDefaultAsync(Guid projectId, string packId, CancellationToken cancellationToken = default);
    Task<RunExportResponse> RunExportAsync(Guid projectId, string exportProfileId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AiProviderStatusDto>> GetAiProvidersAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AiProviderStatusDto>> GetAiProviderStatusesAsync(CancellationToken cancellationToken = default);
    Task<AiProviderConfigurationResultDto> ConfigureAiProviderAsync(AiProviderConfigurationDto configuration, CancellationToken cancellationToken = default);
    Task<AiProviderValidationResultDto> ValidateAiProviderAsync(AiProviderConfigurationDto configuration, int maxOutputTokens = 1200, CancellationToken cancellationToken = default);
    Task<AiProviderStatusDto> TestAiProviderAsync(string providerId, CancellationToken cancellationToken = default);
    Task<AiProviderConnectionDiagnosticDto> DiagnoseAiProviderAsync(string providerId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ContextPackManifest>> GetAiPacksAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<AiPromptPreviewResponse> PreviewAiPromptAsync(Guid projectId, AiPromptPreviewRequest request, CancellationToken cancellationToken = default);
    Task<AiRunDetailDto> StartAiRunAsync(Guid projectId, AiRunRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AiRunSummaryDto>> GetAiRunsAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<AiRunDetailDto?> GetAiRunAsync(Guid projectId, Guid runId, CancellationToken cancellationToken = default);
    Task<AiSettingsDto> GetAiSettingsAsync(CancellationToken cancellationToken = default);
    Task<AiSettingsDto> UpdateAiSettingsAsync(AiSettingsDto settings, CancellationToken cancellationToken = default);
    Task<bool> DeleteAiRunsAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<bool> DeleteAiRunAsync(Guid projectId, Guid runId, CancellationToken cancellationToken = default);
    Task<AiUsageSummaryDto> GetAiUsageAsync(CancellationToken cancellationToken = default);
    Task<AiUsageSummaryDto> GetProjectAiUsageAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<AiContextPackQualityReportDto> GetAiPackQualityAsync(Guid projectId, string packId, CancellationToken cancellationToken = default);
    Task<DogfoodProjectReadinessDto> GetDogfoodStatusAsync(CancellationToken cancellationToken = default);
    Task<DogfoodProjectReadinessDto> GetProjectDogfoodStatusAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<DiagnosticsSummaryDto> GetDiagnosticsSummaryAsync(CancellationToken cancellationToken = default);
    Task<DiagnosticsExportDto> ExportDiagnosticsAsync(CancellationToken cancellationToken = default);
}

public interface ITokenProvider
{
    Task<string?> GetTokenAsync();
}

public sealed class StaticTokenProvider(string? token) : ITokenProvider
{
    public Task<string?> GetTokenAsync() => Task.FromResult(token);
}
