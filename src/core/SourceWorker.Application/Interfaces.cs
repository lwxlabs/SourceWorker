using SourceWorker.Domain;
using SourceWorker.Contracts;

namespace SourceWorker.Application;

public interface IProjectRepository
{
    Task<Project> AddAsync(Project project, CancellationToken cancellationToken);
    Task<Project?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<Project?> GetByPathAsync(string rootPath, CancellationToken cancellationToken);
    Task<IReadOnlyList<Project>> ListAsync(CancellationToken cancellationToken);
    Task<Project> UpdateAsync(Project project, CancellationToken cancellationToken);
}

public interface IScanRunRepository
{
    Task<ScanRun> AddAsync(ScanRun scanRun, CancellationToken cancellationToken);
    Task<ScanRun?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<ScanRun>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken);
    Task<ScanRun?> GetLatestAsync(Guid projectId, CancellationToken cancellationToken);
    Task<ScanRun> UpdateAsync(ScanRun scanRun, CancellationToken cancellationToken);
}

public interface IFileIndexRepository
{
    Task ReplaceForScanAsync(Guid scanRunId, IReadOnlyList<IndexedFile> files, IReadOnlyList<TodoItem> todos, IReadOnlyList<DetectedTechnology> technologies, CancellationToken cancellationToken);
}

public interface IIndexedFileRepository
{
    Task SaveForScanAsync(Guid projectId, Guid scanRunId, IReadOnlyList<IndexedFile> files, CancellationToken cancellationToken);
    Task<IReadOnlyList<IndexedFile>> GetByScanRunAsync(Guid scanRunId, CancellationToken cancellationToken);
    Task<IReadOnlyList<IndexedFile>> GetLatestByProjectAsync(Guid projectId, CancellationToken cancellationToken);
}

public interface IGeneratedArtifactRepository
{
    Task<GeneratedArtifact> UpsertAsync(GeneratedArtifact artifact, CancellationToken cancellationToken);
    Task<IReadOnlyList<GeneratedArtifact>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken);
    Task<GeneratedArtifact?> GetByPathAsync(Guid projectId, string relativePath, CancellationToken cancellationToken);
}

public interface IJobRepository
{
    Task<WorkerJob> AddAsync(WorkerJob job, CancellationToken cancellationToken);
    Task<WorkerJob?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<WorkerJob>> ListAsync(CancellationToken cancellationToken);
    Task<WorkerJob> UpdateAsync(WorkerJob job, CancellationToken cancellationToken);
    Task AddStepAsync(WorkerJobStep step, CancellationToken cancellationToken);
}

public interface IProjectScanner
{
    Task<ProjectScanResult> ScanAsync(Project project, CancellationToken cancellationToken);
}

public interface IGitInspector
{
    Task<GitSnapshot> InspectAsync(Project project, CancellationToken cancellationToken);
}

public interface IContextWriter
{
    Task<ContextWriteResult> WriteBasicContextAsync(ProjectScanResult scanResult, CancellationToken cancellationToken);
}

public interface IRecipeRunner
{
    Task<ContextWriteResult> RunBasicHandoffAsync(Project project, CancellationToken cancellationToken);
    Task<ContextWriteResult> RunBackendDocsCompactAsync(ProjectScanResult scanResult, CancellationToken cancellationToken);
    Task<ContextWriteResult> RunAsync(ProjectRecipe recipe, ProjectScanResult scanResult, ProjectInstructionDocument? instructions, bool force, CancellationToken cancellationToken);
}

public interface IProjectInstructionParser
{
    ProjectInstructionDocument Parse(Guid projectId, string projectRootPath, string instructionFilePath, string markdown);
}

public interface IProjectInstructionRepository
{
    Task<ProjectInstructionDocument?> GetLatestAsync(Guid projectId, CancellationToken cancellationToken);
    Task<ProjectInstructionDocument> SaveAsync(ProjectInstructionDocument document, CancellationToken cancellationToken);
}

public interface IProjectInstructionImporter
{
    Task<ProjectInstructionDocument> ImportAsync(Project project, string instructionFilePath, CancellationToken cancellationToken);
}

public interface IRecipeRepository
{
    Task SaveAsync(Project project, ProjectRecipe recipe, CancellationToken cancellationToken);
    Task<IReadOnlyList<ProjectRecipe>> ListAsync(Project project, CancellationToken cancellationToken);
    Task<ProjectRecipe?> GetAsync(Project project, string recipeId, CancellationToken cancellationToken);
}

public interface IRecipeFactory
{
    IReadOnlyList<ProjectRecipe> CreateDefaultRecipes(Project project);
    ProjectRecipe CreateImportedRecipe(Project project, ProjectInstructionDocument instructions);
}

public interface ISourcePathValidator
{
    IReadOnlyList<ProjectInstructionSourcePath> Validate(string projectRootPath, IEnumerable<string> paths);
}

public interface IInstructionContextWriter
{
    Task WriteImportedInstructionsAsync(Project project, ProjectInstructionDocument instructions, CancellationToken cancellationToken);
    Task WriteSourcePathValidationAsync(Project project, SourcePathValidationReport report, CancellationToken cancellationToken);
}

public interface IDoctorService
{
    Task<DoctorReport> CheckAsync(Project project, CancellationToken cancellationToken);
}

public interface IContextFreshnessService
{
    Task<ContextFreshnessReport> CheckAsync(Project project, CancellationToken cancellationToken);
}

public interface IContextIndexWriter
{
    Task WriteAsync(ProjectScanResult scanResult, ContextFreshnessReport freshness, CancellationToken cancellationToken);
}

public interface IChangeSetService
{
    Task<ChangeSetReport> GetChangesAsync(Project project, CancellationToken cancellationToken);
}

public interface IContextStatusService
{
    Task<ContextFreshnessReport> GetStatusAsync(Project project, CancellationToken cancellationToken);
}

public interface ILocalStorageSchemaService
{
    Task<LocalStorageSchemaInfo> GetInfoAsync(CancellationToken cancellationToken);
}

public interface IStorageInfoService
{
    Task<StorageInfo> GetInfoAsync(CancellationToken cancellationToken);
}

public interface IGeneratedArtifactLifecycleService
{
    Task<ArtifactLifecycleReport> AnalyzeAsync(Project project, CancellationToken cancellationToken);
}

public interface IGeneratedArtifactCleanupService
{
    Task<CleanupPreviewReport> PreviewAsync(Project project, CancellationToken cancellationToken);
    Task<CleanupResult> CleanupAsync(Project project, bool dryRun, bool force, CancellationToken cancellationToken);
}

public interface IArtifactStatusService
{
    Task<ArtifactLifecycleReport> GetArtifactsAsync(Project project, CancellationToken cancellationToken);
}

public interface IStorageHealthService
{
    Task<bool> CheckAsync(CancellationToken cancellationToken);
}

public interface ICSharpCodeIntelligenceScanner
{
    Task<CodeIntelligenceResult> ScanAsync(ProjectScanResult scanResult, CancellationToken cancellationToken);
}

public interface ICodeSymbolRepository { Task SaveAsync(Guid projectId, Guid scanRunId, IReadOnlyList<CodeSymbol> symbols, CancellationToken cancellationToken); Task<IReadOnlyList<CodeSymbol>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken); }
public interface ICodeFlowRepository { Task SaveAsync(Guid projectId, Guid scanRunId, IReadOnlyList<CodeFlow> flows, CancellationToken cancellationToken); Task<IReadOnlyList<CodeFlow>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken); }
public interface IRouteInfoRepository { Task SaveAsync(Guid projectId, Guid scanRunId, IReadOnlyList<RouteInfo> routes, CancellationToken cancellationToken); Task<IReadOnlyList<RouteInfo>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken); }
public interface IServiceRegistrationRepository { Task SaveAsync(Guid projectId, Guid scanRunId, IReadOnlyList<ServiceRegistration> registrations, CancellationToken cancellationToken); Task<IReadOnlyList<ServiceRegistration>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken); }
public interface IConfigurationUsageRepository { Task SaveAsync(Guid projectId, Guid scanRunId, IReadOnlyList<ConfigurationUsage> usages, CancellationToken cancellationToken); Task<IReadOnlyList<ConfigurationUsage>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken); }
public interface IBackgroundWorkerInfoRepository { Task SaveAsync(Guid projectId, Guid scanRunId, IReadOnlyList<BackgroundWorkerInfo> workers, CancellationToken cancellationToken); Task<IReadOnlyList<BackgroundWorkerInfo>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken); }
public interface ITestInfoRepository { Task SaveAsync(Guid projectId, Guid scanRunId, IReadOnlyList<TestInfo> tests, CancellationToken cancellationToken); Task<IReadOnlyList<TestInfo>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken); }

public interface IRouteScanner;
public interface IServiceRegistrationScanner;
public interface IConfigurationUsageScanner;
public interface IBackgroundWorkerScanner;
public interface ITestScanner;
public interface IFlowExtractor { IReadOnlyList<CodeFlow> Extract(ProjectScanResult scan, CodeIntelligenceResult code); }
public interface IFlowDiagramGenerator { string Generate(CodeFlow flow); }
public interface IContextBrowserService
{
    Task<ContextBrowserSummary> GetSummaryAsync(Project project, CancellationToken cancellationToken);
    Task<IReadOnlyList<ContextBrowserFile>> ListFilesAsync(Project project, CancellationToken cancellationToken);
    Task<string> ReadFileAsync(Project project, string relativePath, CancellationToken cancellationToken);
    Task<ContextBrowserQueryResult> SearchAsync(Project project, string query, CancellationToken cancellationToken);
}
public interface IContextSearchService { Task<ContextBrowserQueryResult> SearchAsync(Project project, string query, CancellationToken cancellationToken); }

public interface IOutputProfileService { Task<IReadOnlyList<OutputProfile>> GetProfilesAsync(Project project, CancellationToken cancellationToken); }
public interface IOutputRoutingService { Task<IReadOnlyList<OutputRoute>> GetRoutesAsync(Project project, string recipeId, CancellationToken cancellationToken); }
public interface IGeneratedOutputService { Task<GeneratedOutputSet> GetOutputSetAsync(Project project, string recipeId, CancellationToken cancellationToken); }
public interface IExportProfileService
{
    Task<ExportProfile> WriteBackendDocsProfileAsync(Project project, Guid? scanRunId, IReadOnlyList<GeneratedArtifact> artifacts, CancellationToken cancellationToken);
    Task<IReadOnlyList<ExportProfile>> ListAsync(Project project, CancellationToken cancellationToken);
    Task<ExportProfile?> GetAsync(Project project, string exportProfileId, CancellationToken cancellationToken);
}
public interface IExportRunner { Task<ExportRun> RunAsync(Project project, string exportProfileId, CancellationToken cancellationToken); }
public interface IContextPackService
{
    Task<IReadOnlyList<ContextPackManifest>> ListAsync(Project project, CancellationToken cancellationToken);
    Task<ContextPackManifest?> GetAsync(Project project, string packId, CancellationToken cancellationToken);
}
public interface IContextPackWriter { Task<IReadOnlyList<ContextPackManifest>> WriteDefaultPacksAsync(Project project, Guid? scanRunId, CancellationToken cancellationToken); }
public interface IContextPackReader { Task<string> ReadPackContentAsync(Project project, string packId, CancellationToken cancellationToken); }
public interface IContextPackValidator { Task<ContextPackManifest> ValidateAsync(Project project, ContextPack pack, string relativePath, CancellationToken cancellationToken); }

public sealed record CodeIntelligenceResult(IReadOnlyList<CodeSymbol> Symbols, IReadOnlyList<RouteInfo> Routes, IReadOnlyList<ServiceRegistration> ServiceRegistrations, IReadOnlyList<ConfigurationUsage> ConfigurationUsages, IReadOnlyList<BackgroundWorkerInfo> BackgroundWorkers, IReadOnlyList<TestInfo> Tests, IReadOnlyList<CodeFlow> Flows);

public interface ILocalJobQueue
{
    ValueTask EnqueueAsync(Guid jobId, CancellationToken cancellationToken);
    ValueTask<Guid> DequeueAsync(CancellationToken cancellationToken);
}

public sealed record ProjectScanResult(
    Project Project,
    ScanRun ScanRun,
    IReadOnlyList<IndexedFile> Files,
    IReadOnlyList<TodoItem> Todos,
    IReadOnlyList<DetectedTechnology> Technologies,
    GitSnapshot Git,
    string? SolutionName,
    string? SolutionPath,
    IReadOnlyList<SolutionFile> SolutionFiles,
    IReadOnlyList<ProjectFile> Projects,
    IReadOnlyList<string> ReadmeFiles,
    IReadOnlyList<string> ConfigFiles,
    IReadOnlyList<string> DockerFiles,
    IReadOnlyList<FolderRole> FolderRoles,
    IReadOnlyList<string> MainEntrypoints,
    IReadOnlyList<string> DeletedFiles);
public sealed record ContextWriteResult(string OutputPath, IReadOnlyList<string> Files);

public interface ILocalApiRuntimeService
{
    LocalApiRuntimeState GetState();
}

public interface ILocalApiTokenService
{
    string? GetCurrentToken();
    string GenerateAndSaveToken();
    bool ValidateToken(string token);
    DateTimeOffset? GetTokenCreatedUtc();
}

public interface ILocalApiOriginService
{
    bool IsOriginAllowed(string? origin);
    string[] GetAllowedOrigins();
}

public interface IAppVersionService
{
    VersionInfoDto GetVersion();
}

public interface IAiProvider
{
    string Id { get; }
    AiProviderKind Kind { get; }
    Task<AiProviderHealthResult> CheckHealthAsync(AiProviderConfiguration configuration, CancellationToken cancellationToken);
    Task<AiRunOutput> RunAsync(AiProviderConfiguration configuration, string prompt, int maxOutputTokens, CancellationToken cancellationToken);
}

public interface IAiProviderRegistry
{
    IReadOnlyList<IAiProvider> ListProviders();
    IAiProvider? GetProvider(string providerId);
}

public interface IAiProviderConfigurationRepository
{
    Task<AiProviderConfiguration> SaveAsync(AiProviderConfiguration configuration, CancellationToken cancellationToken);
    Task<AiProviderConfiguration?> GetAsync(string providerId, CancellationToken cancellationToken);
    Task<IReadOnlyList<AiProviderConfiguration>> ListAsync(CancellationToken cancellationToken);
}

public interface IAiModelCatalogService { IReadOnlyList<AiModelInfo> GetModels(AiProviderKind providerKind); }
public interface IAiModelCatalogProvider { Task<AiModelCatalog> GetCatalogAsync(AiProviderKind providerKind, CancellationToken cancellationToken); }
public interface IAiPromptBuilder { Task<AiPromptPreview> BuildPreviewAsync(Guid projectId, string providerId, string packId, string? userInstruction, int maxOutputTokens, string? templateId, CancellationToken cancellationToken); }
public interface IAiPromptPreviewService { Task<AiPromptPreview> PreviewAsync(Guid projectId, string providerId, string packId, string? userInstruction, int maxOutputTokens, string? templateId, CancellationToken cancellationToken); }
public interface IAiTokenEstimator { AiTokenEstimate Estimate(string prompt, int maxOutputTokens); AiCostEstimate EstimateCost(AiTokenEstimate tokens, AiModelInfo? model); }
public interface IAiRunRepository
{
    Task<AiRun> SaveAsync(AiRun run, CancellationToken cancellationToken);
    Task<AiRun?> GetAsync(Guid runId, CancellationToken cancellationToken);
    Task<IReadOnlyList<AiRun>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken);
    Task DeleteAsync(Guid runId, CancellationToken cancellationToken);
    Task DeleteByProjectAsync(Guid projectId, CancellationToken cancellationToken);
}
public interface IAiRunService
{
    Task<AiRun> StartAsync(AiRunInput input, CancellationToken cancellationToken);
    Task<IReadOnlyList<AiRun>> GetRunsAsync(Guid projectId, CancellationToken cancellationToken);
    Task<AiRun?> GetRunAsync(Guid runId, CancellationToken cancellationToken);
}
public interface IAiSafetyService { AiRunSafetyCheck Check(string content, ContextPackManifest? pack = null, ContextFreshnessReport? freshness = null); }
public interface IAiRedactionService { AiRedactionResult Redact(string content); }
public interface IAiSecretStore
{
    Task SaveSecretAsync(string secretName, string secretValue, CancellationToken cancellationToken);
    Task<string?> GetSecretAsync(string secretName, CancellationToken cancellationToken);
    string Mask(string? secretValue);
}

public interface IAiProviderValidationService
{
    Task<AiProviderValidationResult> ValidateAsync(AiProviderKind kind, string model, string? baseUrl, string? apiKey, int maxOutputTokens, CancellationToken cancellationToken);
    Task<AiProviderConnectionDiagnostic> DiagnoseAsync(string providerId, CancellationToken cancellationToken);
}

public interface IAiSettingsRepository
{
    Task<AiSettings> GetAsync(CancellationToken cancellationToken);
    Task<AiSettings> SaveAsync(AiSettings settings, CancellationToken cancellationToken);
}

public interface IAiUsageService
{
    Task<AiUsageSummary> GetGlobalAsync(CancellationToken cancellationToken);
    Task<AiUsageSummary> GetProjectAsync(Guid projectId, CancellationToken cancellationToken);
}

public interface IAiContextPackQualityService
{
    Task<AiContextPackQualityReport> CheckAsync(Guid projectId, string packId, CancellationToken cancellationToken);
}

public interface IDogfoodReadinessService
{
    Task<DogfoodProjectReadiness> GetGlobalStatusAsync(CancellationToken cancellationToken);
    Task<DogfoodProjectReadiness> GetProjectStatusAsync(Guid projectId, CancellationToken cancellationToken);
}

public interface IDiagnosticsService
{
    Task<DiagnosticsSummary> GetSummaryAsync(CancellationToken cancellationToken);
    Task<DiagnosticsExport> ExportAsync(CancellationToken cancellationToken);
}

public interface ISecretStore
{
    Task SaveAsync(string name, string value, CancellationToken cancellationToken);
    Task<string?> GetAsync(string name, CancellationToken cancellationToken);
    Task ClearAsync(string name, CancellationToken cancellationToken);
}

public interface ISecretStoreProvider { string StorageKind { get; } ISecretStore Store { get; } }
public interface ISecretStoreHealthService { Task<SecretStoreHealth> CheckAsync(CancellationToken cancellationToken); }
public interface ISecretMetadataService { Task<SecretMetadata> GetMetadataAsync(string name, CancellationToken cancellationToken); }
