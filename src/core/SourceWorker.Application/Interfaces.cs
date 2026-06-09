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
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
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

public interface ICodeIntelligencePipeline
{
    Task<CodeIntelligenceResult> AnalyzeAsync(ProjectScanResult scanResult, CodeAnalysisSettings settings, CancellationToken cancellationToken);
}

public interface ICodeAnalyzer
{
    CodeAnalyzerDescriptor Descriptor { get; }
    bool CanAnalyze(ProjectScanResult scanResult);
    Task<CodeAnalyzerResult> AnalyzeAsync(ProjectScanResult scanResult, CodeAnalysisSettings settings, CancellationToken cancellationToken);
}

public interface ICodeAnalyzerRegistry
{
    IReadOnlyList<CodeAnalyzerDescriptor> ListDescriptors();
    IReadOnlyList<ICodeAnalyzer> ListAnalyzers();
    ICodeAnalyzer? GetAnalyzer(string analyzerId);
}

public interface ICodeAnalysisSettingsService
{
    Task<CodeAnalysisSettings> GetAsync(Guid projectId, CancellationToken cancellationToken);
    Task<CodeAnalysisSettings> SaveAsync(CodeAnalysisSettings settings, CancellationToken cancellationToken);
}

public interface ILanguageDetectionService
{
    ProjectLanguageProfile Detect(ProjectScanResult scanResult, IReadOnlyList<CodeAnalyzerResult> analyzerResults);
}

public interface ICodeSymbolRepository { Task SaveAsync(Guid projectId, Guid scanRunId, IReadOnlyList<CodeSymbol> symbols, CancellationToken cancellationToken); Task<IReadOnlyList<CodeSymbol>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken); }
public interface ICodeFlowRepository { Task SaveAsync(Guid projectId, Guid scanRunId, IReadOnlyList<CodeFlow> flows, CancellationToken cancellationToken); Task<IReadOnlyList<CodeFlow>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken); }
public interface IRouteInfoRepository { Task SaveAsync(Guid projectId, Guid scanRunId, IReadOnlyList<RouteInfo> routes, CancellationToken cancellationToken); Task<IReadOnlyList<RouteInfo>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken); }
public interface IServiceRegistrationRepository { Task SaveAsync(Guid projectId, Guid scanRunId, IReadOnlyList<ServiceRegistration> registrations, CancellationToken cancellationToken); Task<IReadOnlyList<ServiceRegistration>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken); }
public interface IConfigurationUsageRepository { Task SaveAsync(Guid projectId, Guid scanRunId, IReadOnlyList<ConfigurationUsage> usages, CancellationToken cancellationToken); Task<IReadOnlyList<ConfigurationUsage>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken); }
public interface IBackgroundWorkerInfoRepository { Task SaveAsync(Guid projectId, Guid scanRunId, IReadOnlyList<BackgroundWorkerInfo> workers, CancellationToken cancellationToken); Task<IReadOnlyList<BackgroundWorkerInfo>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken); }
public interface ITestInfoRepository { Task SaveAsync(Guid projectId, Guid scanRunId, IReadOnlyList<TestInfo> tests, CancellationToken cancellationToken); Task<IReadOnlyList<TestInfo>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken); }
public interface ICodeAnalyzerRunRepository { Task ReplaceForScanAsync(Guid projectId, Guid scanRunId, IReadOnlyList<CodeAnalyzerRun> runs, CancellationToken cancellationToken); Task<IReadOnlyList<CodeAnalyzerRun>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken); }
public interface ICodeComponentRepository { Task SaveAsync(Guid projectId, Guid scanRunId, IReadOnlyList<CodeComponent> components, CancellationToken cancellationToken); Task<IReadOnlyList<CodeComponent>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken); }
public interface IFrontendRouteRepository { Task SaveAsync(Guid projectId, Guid scanRunId, IReadOnlyList<FrontendRoute> routes, CancellationToken cancellationToken); Task<IReadOnlyList<FrontendRoute>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken); }
public interface IScriptImportRepository { Task SaveAsync(Guid projectId, Guid scanRunId, IReadOnlyList<ScriptImport> imports, CancellationToken cancellationToken); Task<IReadOnlyList<ScriptImport>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken); }
public interface IStyleUsageRepository { Task SaveAsync(Guid projectId, Guid scanRunId, IReadOnlyList<StyleUsage> styles, CancellationToken cancellationToken); Task<IReadOnlyList<StyleUsage>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken); }
public interface IPackageReferenceRepository { Task SaveAsync(Guid projectId, Guid scanRunId, IReadOnlyList<PackageReferenceInfo> packages, CancellationToken cancellationToken); Task<IReadOnlyList<PackageReferenceInfo>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken); }
public interface IBuildScriptRepository { Task SaveAsync(Guid projectId, Guid scanRunId, IReadOnlyList<BuildScriptInfo> scripts, CancellationToken cancellationToken); Task<IReadOnlyList<BuildScriptInfo>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken); }
public interface IProjectLanguageProfileRepository { Task SaveAsync(ProjectLanguageProfile profile, CancellationToken cancellationToken); Task<ProjectLanguageProfile?> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken); }
public interface ICodeAnalysisSettingsRepository { Task<CodeAnalysisSettings?> GetAsync(Guid projectId, CancellationToken cancellationToken); Task<CodeAnalysisSettings> SaveAsync(CodeAnalysisSettings settings, CancellationToken cancellationToken); }

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
    Task<IReadOnlyList<ContextBrowserFile>> ListFilesAsync(Project project, bool showTechnical, bool showExports, CancellationToken cancellationToken);
    Task<string> ReadFileAsync(Project project, string relativePath, CancellationToken cancellationToken);
    Task<ContextBrowserQueryResult> SearchAsync(Project project, string query, CancellationToken cancellationToken);
}
public interface IContextSearchService { Task<ContextBrowserQueryResult> SearchAsync(Project project, string query, CancellationToken cancellationToken); }
public interface IAgentHandoffService { Task<AgentHandoff> GetAsync(Project project, CancellationToken cancellationToken); }
public interface IScanCompletenessService { Task<ScanCompletenessReport> GetAsync(Project project, CancellationToken cancellationToken); }
public interface IMarkdownWorkspaceService
{
    Task<IReadOnlyList<MarkdownFileSummary>> ListFilesAsync(Project project, MarkdownWorkspaceScope? scope, bool includeTechnical, bool includeExternalMirror, bool includeUserMarkdown, CancellationToken cancellationToken);
    Task<MarkdownFileContent> ReadAsync(Project project, string relativePath, CancellationToken cancellationToken);
    Task<MarkdownFileSaveResult> SaveAsync(Project project, MarkdownFileSaveRequest request, CancellationToken cancellationToken);
    MarkdownValidationResult Validate(string markdown);
}
public interface IMarkdownFileReader { Task<MarkdownFileContent> ReadAsync(Project project, string relativePath, CancellationToken cancellationToken); }
public interface IMarkdownFileWriter { Task<MarkdownFileSaveResult> SaveAsync(Project project, MarkdownFileSaveRequest request, CancellationToken cancellationToken); }
public interface IMarkdownSavePolicyService { MarkdownValidationResult ValidatePath(Project project, string relativePath, bool allowTechnical); }
public interface IMermaidMarkdownValidator { MarkdownValidationResult Validate(string markdown); }

public interface ISourceContextBranchRepository
{
    Task<SourceContextBranch> SaveAsync(SourceContextBranch branch, CancellationToken cancellationToken);
    Task<SourceContextBranch?> GetAsync(Guid projectId, Guid branchId, CancellationToken cancellationToken);
    Task<SourceContextBranch?> GetByNameAsync(Guid projectId, string name, CancellationToken cancellationToken);
    Task<IReadOnlyList<SourceContextBranch>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken);
}

public interface ISourceSnapshotRepository
{
    Task<SourceSnapshot> SaveAsync(SourceSnapshot snapshot, CancellationToken cancellationToken);
    Task<SourceSnapshot?> GetAsync(Guid projectId, Guid snapshotId, CancellationToken cancellationToken);
    Task<SourceSnapshot?> GetByScanRunAsync(Guid projectId, Guid scanRunId, CancellationToken cancellationToken);
    Task<IReadOnlyList<SourceSnapshot>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken);
    Task<IReadOnlyList<SourceSnapshot>> GetByBranchAsync(Guid projectId, Guid branchId, CancellationToken cancellationToken);
    Task DeleteAsync(Guid projectId, Guid snapshotId, CancellationToken cancellationToken);
}

public interface ISourceSnapshotFileRepository
{
    Task ReplaceForSnapshotAsync(Guid projectId, Guid snapshotId, IReadOnlyList<SourceSnapshotFile> files, CancellationToken cancellationToken);
    Task<IReadOnlyList<SourceSnapshotFile>> GetBySnapshotAsync(Guid projectId, Guid snapshotId, CancellationToken cancellationToken);
}

public interface ISourceSnapshotMetricRepository
{
    Task ReplaceForSnapshotAsync(Guid snapshotId, IReadOnlyList<SourceSnapshotMetric> metrics, CancellationToken cancellationToken);
    Task<IReadOnlyList<SourceSnapshotMetric>> GetBySnapshotAsync(Guid snapshotId, CancellationToken cancellationToken);
}

public interface ISourceContextBranchService
{
    Task<IReadOnlyList<SourceContextBranch>> GetBranchesAsync(Guid projectId, CancellationToken cancellationToken);
    Task<SourceContextBranch?> GetBranchAsync(Guid projectId, Guid branchId, CancellationToken cancellationToken);
    Task<SourceContextBranch> CreateBranchAsync(Guid projectId, string name, string? description, CancellationToken cancellationToken);
    Task<SourceContextBranch> RenameBranchAsync(Guid projectId, Guid branchId, string name, CancellationToken cancellationToken);
    Task<SourceContextBranch> UpdateBranchDescriptionAsync(Guid projectId, Guid branchId, string? description, CancellationToken cancellationToken);
    Task<SourceContextBranch> ActivateBranchAsync(Guid projectId, Guid branchId, CancellationToken cancellationToken);
    Task<bool> RemoveBranchAsync(Guid projectId, Guid branchId, CancellationToken cancellationToken);
    Task<SourceContextBranch> GetActiveBranchAsync(Guid projectId, CancellationToken cancellationToken);
    Task<SourceContextBranch> EnsureDefaultBranchAsync(Guid projectId, CancellationToken cancellationToken);
}

public interface ISourceSnapshotService
{
    Task<SourceSnapshot> CreateSnapshotFromScanAsync(Guid projectId, Guid scanRunId, CancellationToken cancellationToken);
    Task<IReadOnlyList<SourceSnapshot>> GetSnapshotsAsync(Guid projectId, Guid? branchId, CancellationToken cancellationToken);
    Task<SourceSnapshot?> GetSnapshotAsync(Guid projectId, Guid snapshotId, CancellationToken cancellationToken);
    Task<SourceSnapshot?> GetLatestSnapshotAsync(Guid projectId, CancellationToken cancellationToken);
    Task<SourceSnapshot?> GetLatestSnapshotForBranchAsync(Guid projectId, Guid branchId, CancellationToken cancellationToken);
    Task<SourceSnapshot?> GetPreviousSnapshotAsync(Guid projectId, Guid snapshotId, CancellationToken cancellationToken);
    Task DeleteSnapshotAsync(Guid projectId, Guid snapshotId, CancellationToken cancellationToken);
}

public interface ISourceDiffService
{
    Task<SourceDiffReport> CompareSnapshotsAsync(Guid projectId, Guid leftSnapshotId, Guid rightSnapshotId, CancellationToken cancellationToken);
    Task<SourceDiffReport> CompareLatestWithPreviousAsync(Guid projectId, Guid branchId, CancellationToken cancellationToken);
    Task<SourceDiffReport> CompareBranchLatestSnapshotsAsync(Guid projectId, Guid leftBranchId, Guid rightBranchId, CancellationToken cancellationToken);
    Task<SourceTextDiff> CompareFilesAsync(Guid projectId, Guid leftSnapshotId, Guid rightSnapshotId, string relativePath, CancellationToken cancellationToken);
}

public interface ITextDiffService
{
    string CreateUnifiedDiff(string? leftText, string? rightText, string path);
    SourceTextDiff CreateSideBySideDiff(string? leftText, string? rightText, string path);
    SourceTextDiff CreateSideBySideDiff(string? leftText, string? rightText, string path, string? projectRoot);
    string CreateMarkdownDiff(string? leftText, string? rightText, string path);
}

public interface IBranchDiffPackService
{
    Task<BranchDiffPack> BuildBranchDiffPackAsync(Guid projectId, Guid leftSnapshotId, Guid rightSnapshotId, CancellationToken cancellationToken);
    Task<BranchDiffPack> BuildLatestBranchDiffPackAsync(Guid projectId, Guid branchId, CancellationToken cancellationToken);
    Task<BranchDiffPack> SaveBranchDiffPackAsync(Guid projectId, Guid leftSnapshotId, Guid rightSnapshotId, CancellationToken cancellationToken);
    Task<BranchDiffPack?> BuildScanDiffAsync(Guid projectId, Guid branchId, SourceBranchScanDiffScope scope, CancellationToken cancellationToken);
    Task<BranchDiffPack?> SaveScanDiffAsync(Guid projectId, Guid branchId, SourceBranchScanDiffScope scope, CancellationToken cancellationToken);
    Task<BranchDiffPack?> SaveLatestTenScanDiffAsync(Guid projectId, Guid branchId, CancellationToken cancellationToken);
}

public interface IOptionalGitMetadataService
{
    Task<OptionalGitMetadata?> TryGetGitMetadataAsync(string projectRoot, CancellationToken cancellationToken);
}

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

public sealed record CodeIntelligenceResult
{
    public CodeIntelligenceResult(IReadOnlyList<CodeSymbol> symbols, IReadOnlyList<RouteInfo> routes, IReadOnlyList<ServiceRegistration> serviceRegistrations, IReadOnlyList<ConfigurationUsage> configurationUsages, IReadOnlyList<BackgroundWorkerInfo> backgroundWorkers, IReadOnlyList<TestInfo> tests, IReadOnlyList<CodeFlow> flows)
    {
        Symbols = symbols;
        Routes = routes;
        ServiceRegistrations = serviceRegistrations;
        ConfigurationUsages = configurationUsages;
        BackgroundWorkers = backgroundWorkers;
        Tests = tests;
        Flows = flows;
    }

    public IReadOnlyList<CodeSymbol> Symbols { get; init; }
    public IReadOnlyList<RouteInfo> Routes { get; init; }
    public IReadOnlyList<ServiceRegistration> ServiceRegistrations { get; init; }
    public IReadOnlyList<ConfigurationUsage> ConfigurationUsages { get; init; }
    public IReadOnlyList<BackgroundWorkerInfo> BackgroundWorkers { get; init; }
    public IReadOnlyList<TestInfo> Tests { get; init; }
    public IReadOnlyList<CodeFlow> Flows { get; init; }
    public IReadOnlyList<CodeComponent> Components { get; init; } = [];
    public IReadOnlyList<FrontendRoute> FrontendRoutes { get; init; } = [];
    public IReadOnlyList<StyleUsage> Styles { get; init; } = [];
    public IReadOnlyList<ScriptImport> Imports { get; init; } = [];
    public IReadOnlyList<PackageReferenceInfo> PackageReferences { get; init; } = [];
    public IReadOnlyList<BuildScriptInfo> BuildScripts { get; init; } = [];
    public IReadOnlyList<CodeAnalyzerRun> AnalyzerRuns { get; init; } = [];
    public ProjectLanguageProfile? LanguageProfile { get; init; }
    public IReadOnlyList<DetectedTechnology> Technologies { get; init; } = [];
}

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

public interface IAiStreamingProvider : IAiProvider
{
    IAsyncEnumerable<AiProviderStreamUpdate> StreamAsync(AiProviderConfiguration configuration, string prompt, int maxOutputTokens, CancellationToken cancellationToken);
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
public interface IAiProviderCatalogService
{
    AiProviderCatalog GetCatalog();
    IReadOnlyList<AiProviderPresetInfo> GetPresets();
    Task<AiProviderModelList> GetModelsAsync(string providerId, CancellationToken cancellationToken);
}
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

public interface IAiExplanationPromptBuilder
{
    Task<AiExplanationPreview> BuildPreviewAsync(AiExplanationRequest request, CancellationToken cancellationToken);
}

public interface IAiExplanationQualityService
{
    AiExplanationQualityReport Check(string markdown);
}

public interface IAiExplanationService
{
    Task<AiExplanationOptions> GetOptionsAsync(Guid projectId, CancellationToken cancellationToken);
    Task<AiExplanationPreview> PreviewAsync(AiExplanationRequest request, CancellationToken cancellationToken);
    Task<AiExplanationResult> RunAsync(AiExplanationRequest request, CancellationToken cancellationToken);
    IAsyncEnumerable<AiExplanationStreamUpdate> RunStreamingAsync(AiExplanationRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<AiExplanationResult>> GetRunsAsync(Guid projectId, CancellationToken cancellationToken);
    Task<AiExplanationResult?> GetRunAsync(Guid projectId, Guid runId, CancellationToken cancellationToken);
    Task<AiExplanationSaveResult> SaveAsync(AiExplanationSaveRequest request, CancellationToken cancellationToken);
    Task<bool> DeleteRunAsync(Guid projectId, Guid runId, CancellationToken cancellationToken);
}

public sealed record AiExplanationOptions(
    Guid ProjectId,
    IReadOnlyList<AiProviderHealthResult> Providers,
    IReadOnlyList<ContextPackManifest> Packs,
    string SuggestedFilePath,
    IReadOnlyList<ExplanationProfile> Profiles);

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

public interface IDogfoodSessionRepository
{
    Task<DogfoodSession> SaveAsync(DogfoodSession session, CancellationToken cancellationToken);
    Task<DogfoodSession?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<DogfoodSession>> ListAsync(CancellationToken cancellationToken);
}

public interface IDogfoodIssueRepository
{
    Task<DogfoodSessionIssue> SaveAsync(DogfoodSessionIssue issue, CancellationToken cancellationToken);
    Task<IReadOnlyList<DogfoodSessionIssue>> ListAsync(CancellationToken cancellationToken);
    Task<DogfoodSessionIssue?> GetAsync(Guid id, CancellationToken cancellationToken);
}

public interface IDogfoodSessionService
{
    Task<DogfoodSession> StartAsync(IReadOnlyList<DogfoodSessionProject> projects, CancellationToken cancellationToken);
    Task<DogfoodSession?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<DogfoodSession>> ListAsync(CancellationToken cancellationToken);
    Task<DogfoodSession> CompleteStepAsync(Guid sessionId, string stepId, CancellationToken cancellationToken);
    Task<DogfoodSession> FinishAsync(Guid sessionId, CancellationToken cancellationToken);
    Task<DiagnosticsExport> ExportAsync(Guid sessionId, CancellationToken cancellationToken);
}

public interface IFrictionIssueService
{
    Task<DogfoodSessionIssue> ReportAsync(DogfoodSessionIssue issue, CancellationToken cancellationToken);
    Task<IReadOnlyList<DogfoodSessionIssue>> ListAsync(CancellationToken cancellationToken);
    Task<DogfoodSessionIssue?> UpdateStatusAsync(Guid issueId, string status, CancellationToken cancellationToken);
    Task<DiagnosticsExport> ExportIssuesAsync(CancellationToken cancellationToken);
}

public interface INextActionService
{
    Task<IReadOnlyList<NextAction>> GetGlobalAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<NextAction>> GetProjectAsync(Guid projectId, CancellationToken cancellationToken);
}

public interface IProjectPathValidationService
{
    Task<ProjectPathValidationResult> ValidateAsync(string path, CancellationToken cancellationToken);
    Task<IReadOnlyList<RecentProjectPath>> GetRecentAsync(CancellationToken cancellationToken);
}

public interface IProjectAiSettingsRepository
{
    Task<ProjectAiSettings> GetAsync(Guid projectId, CancellationToken cancellationToken);
    Task<ProjectAiSettings> SaveAsync(ProjectAiSettings settings, CancellationToken cancellationToken);
}

public interface IQuickAskService
{
    Task<QuickAskResult> PreviewAsync(QuickAskRequest request, CancellationToken cancellationToken);
    Task<QuickAskResult> RunAsync(QuickAskRequest request, CancellationToken cancellationToken);
}

public interface ILogRepository
{
    Task<LogEntry> AddAsync(LogEntry entry, CancellationToken cancellationToken);
    Task<IReadOnlyList<LogEntry>> ListAsync(CancellationToken cancellationToken);
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
