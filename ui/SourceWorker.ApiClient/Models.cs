using System.Text.Json.Serialization;

namespace SourceWorker.ApiClient;

public sealed record HealthResponse(string Status, string Service);
public sealed record ProjectDto(Guid Id, string Name, string RootPath, DateTimeOffset CreatedAt, DateTimeOffset? LastScannedAt);
public sealed record CreateProjectResponse(ProjectDto Project);
public sealed record LocalStorageSchemaInfo(string Provider, string Mode, string StoragePath, int SchemaVersion, DateTimeOffset CreatedUtc, DateTimeOffset LastMigratedUtc, string AppStorageFormatVersion);
public sealed record StorageInfo(LocalStorageSchemaInfo Schema, int ProjectCount, int ScanRunCount, int IndexedFileCount, int GeneratedArtifactCount, int JobCount, int InstructionCount, int RecipeCount, bool RepositoryHealth, int CodeSymbolCount, int CodeFlowCount, int RouteCount, int ServiceRegistrationCount, int ConfigurationUsageCount, int TestInfoCount);
public sealed record DoctorReport(string ProjectRoot, bool ProjectRootFound, bool GitRepoFound, IReadOnlyList<string> SolutionFiles, IReadOnlyList<string> ProjectFiles, IReadOnlyList<string> TestProjects, bool ContextFolderExists, bool ImportedInstructionsFound, IReadOnlyList<string> Recipes, IReadOnlyList<ProjectInstructionSourcePath> MissingSourcePaths, bool IsContextStale, DateTimeOffset? LastScanTime, IReadOnlyList<string> Warnings);
public sealed record ProjectInstructionSourcePath(string OriginalPath, string NormalizedPath, bool Exists, string Type, bool IsInsideProject, string? WarningMessage);
public sealed record ContextFreshnessReport(Guid ProjectId, bool HasStaleContext, IReadOnlyList<string> StaleContextFiles, IReadOnlyList<string> MissingGeneratedFiles, IReadOnlyList<string> ManuallyModifiedFiles, IReadOnlyList<string> FilesChangedSinceLastGeneration, int FreshnessScore, IReadOnlyList<string> RecommendedActions);
public sealed record GeneratedArtifact(Guid Id, Guid ProjectId, Guid? ScanRunId, string? RecipeId, string RelativePath, string AbsolutePath, string Type, string ContentHash, DateTimeOffset GeneratedUtc, string SourceInputHash, string Status, string GeneratorName, string? Notes);
public sealed record ArtifactLifecycleReport(Guid ProjectId, IReadOnlyList<GeneratedArtifact> Artifacts, IReadOnlyList<string> MissingArtifacts, IReadOnlyList<string> ManuallyModifiedArtifacts, IReadOnlyList<string> StaleArtifacts, IReadOnlyList<string> OrphanedArtifacts, IReadOnlyList<string> DeprecatedArtifacts, bool CleanupRecommended);
public sealed record CleanupPreviewReport(Guid ProjectId, IReadOnlyList<string> SafeDeleteFiles, IReadOnlyList<string> MissingTrackedArtifacts, IReadOnlyList<string> StaleTrackedArtifacts, IReadOnlyList<string> Warnings);
public sealed record CodeSymbol(Guid Id, Guid ProjectId, Guid ScanRunId, string FilePath, string RelativePath, string? Namespace, string Name, string FullName, string Kind, string Accessibility, string? ParentSymbolName, string? ReturnType, IReadOnlyList<string> Parameters, int LineNumber, IReadOnlyList<string> Attributes, bool IsStatic, bool IsAsync);
public sealed record CodeFlow(Guid Id, Guid ProjectId, Guid ScanRunId, string Name, string FlowType, string Trigger, IReadOnlyList<string> InvolvedFiles, IReadOnlyList<string> InvolvedSymbols, IReadOnlyList<CodeFlowStep> Steps, string Confidence, IReadOnlyList<string> Warnings);
public sealed record CodeFlowStep(int Order, string Text, string? SymbolName, string? RelativePath);
public sealed record RouteInfo(Guid Id, Guid ProjectId, Guid ScanRunId, string RelativePath, string HttpMethod, string RoutePattern, string? HandlerSymbol, int LineNumber);
public sealed record ServiceRegistration(Guid Id, Guid ProjectId, Guid ScanRunId, string RelativePath, string RegistrationMethod, string ServiceType, string? ImplementationType, int LineNumber);
public sealed record ConfigurationUsage(Guid Id, Guid ProjectId, Guid ScanRunId, string RelativePath, string KeyOrSection, string UsageKind, int LineNumber);
public sealed record TestInfo(Guid Id, Guid ProjectId, Guid ScanRunId, string RelativePath, string ClassName, string MethodName, string TestFramework, int LineNumber);
public sealed record ContextBrowserFile(string RelativePath, string AbsolutePath, string Kind, long SizeBytes, DateTimeOffset LastModifiedUtc);
public sealed record ContextBrowserSummary(Guid ProjectId, IReadOnlyList<ContextBrowserFile> Files, IReadOnlyList<ProjectRecipe> Recipes, IReadOnlyList<CodeFlow> Flows, IReadOnlyList<RouteInfo> Routes, IReadOnlyList<object> Todos, IReadOnlyList<GeneratedArtifact> ProblemArtifacts);
public sealed record ProjectRecipe(string Id, string Name, string Description, string Language, string OutputPath, string TargetPlatform, IReadOnlyList<string> IncludedSections, IReadOnlyList<string> SourceScopes, IReadOnlyList<string> Rules, IReadOnlyList<string> SkipItems, IReadOnlyList<string> VerificationChecklist, IReadOnlyList<string> GeneratedFiles, bool MermaidEnabled, bool RequireClassAndMethodNames, bool RequireSourceReferences, IReadOnlyDictionary<string, IReadOnlyList<string>> ExtraSections);
public sealed record OutputDestination(string Id, string Type, string Path, bool Primary, string PublishMode);
public sealed record GeneratedOutputSet(string RecipeId, IReadOnlyList<OutputDestination> Destinations, IReadOnlyList<GeneratedOutputFile> Files, IReadOnlyList<string> Warnings);
public sealed record GeneratedOutputFile(string RelativePath, string DestinationId, string ArtifactType, string Status, bool Exists);
public sealed record ContextPackManifest(ContextPack Pack, string RelativePath, IReadOnlyList<string> MissingFiles, bool IsValid);
public sealed record ContextPack(string Id, string Name, string Description, string Audience, string Purpose, IReadOnlyList<ContextPackItem> Items, IReadOnlyList<string> IncludedSelectors, string MaxRecommendedUse, int TokenEstimate, string FreshnessStatus, Guid? SourceScanRunId, DateTimeOffset GeneratedUtc);
public sealed record ContextPackItem(string RelativePath, string Kind, bool Required);
public sealed record ExportRun(string Id, string ExportProfileId, Guid ProjectId, DateTimeOffset StartedUtc, DateTimeOffset FinishedUtc, IReadOnlyList<string> ExportedFiles, IReadOnlyList<string> Warnings);

public sealed record ProjectSummaryDto(
    Guid Id,
    string Name,
    string Path,
    bool Exists,
    DateTimeOffset? LastScanUtc,
    int ContextFreshnessScore,
    bool HasStaleContext,
    bool HasManualGeneratedFiles,
    int TechnologyCount,
    int RouteCount,
    int FlowCount
);

public sealed record ApiRuntimeInfoDto(
    string SelectedHost,
    int SelectedPort,
    string BaseUrl,
    bool TokenEnabled,
    DateTimeOffset? TokenCreatedUtc,
    string[] AllowedOrigins,
    string? DashboardOrigin,
    int ProcessId,
    DateTimeOffset StartupUtc,
    string EnvironmentName
);

public sealed record ApiSecurityInfoDto(
    bool TokenEnabled,
    DateTimeOffset? TokenCreatedUtc,
    string[] AllowedOrigins,
    string? DashboardOrigin
);

public sealed record VersionInfoDto(
    string AppName,
    string AppVersion,
    string ApiVersion,
    string SupportedApiClientVersion,
    int StorageSchemaVersion,
    int ContextFormatVersion,
    string BuildConfiguration
);

public sealed record ApiErrorInfo(string Message, string? Code = null, string? Detail = null);
public sealed record ApiConnectionStatus(bool Connected, string? Error = null, ApiRuntimeInfoDto? Runtime = null);

public sealed record JobStatusDto(Guid Id, Guid? ProjectId, string Type, string Status, int ProgressPercent, string? Message, string? CurrentStep, string? ErrorMessage, IReadOnlyList<string> Logs);

public sealed record StartScanResponse(Guid JobId);
public sealed record RunExportResponse(string ExportProfileId, string OutputPath, IReadOnlyList<string> Files);
public sealed record ExportPackResponse(string PackId, string OutputPath, bool Success);

public sealed record CleanupCandidateDto(string RelativePath, string Kind, bool SafeToDelete);
public sealed record CleanupPreviewDto(Guid ProjectId, IReadOnlyList<CleanupCandidateDto> Candidates, int SafeToDeleteCount, string RecommendedCommand);
public sealed record CleanupGeneratedResponse(Guid ProjectId, bool DryRun, IReadOnlyList<string> DeletedFiles, IReadOnlyList<string> SkippedFiles);

public enum DashboardActionKind
{
    ScanProject,
    RunRecipe,
    ExportDocs,
    ExportPack,
    CleanupPreview,
    CleanupGenerated,
    CleanupForce,
    RegisterProject
}

public enum DashboardActionSafetyLevel
{
    ReadOnly,
    SafeWrite,
    GeneratedFileWrite,
    DestructiveGeneratedCleanup,
    DangerousDisabled
}

public sealed record DashboardActionAvailability(
    DashboardActionKind Kind,
    DashboardActionSafetyLevel SafetyLevel,
    bool IsEnabled,
    string? DisabledReason = null,
    DashboardActionRequirement[]? Requirements = null
);

public sealed record DashboardActionRequirement(string Key, string Description, bool IsMet);
