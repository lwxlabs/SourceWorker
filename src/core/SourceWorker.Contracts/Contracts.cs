using SourceWorker.Domain;

namespace SourceWorker.Contracts;

public sealed record ProjectDto(Guid Id, string Name, string RootPath, DateTimeOffset CreatedAt, DateTimeOffset? LastScannedAt);
public sealed record AddProjectRequest(string Path, string? Name);
public sealed record CreateProjectResponse(ProjectDto Project);
public sealed record StartScanResponse(Guid JobId);
public sealed record JobStatusDto(Guid Id, Guid? ProjectId, WorkerJobType Type, WorkerJobStatus Status, int ProgressPercent, string? Message, string? CurrentStep, string? ErrorMessage, IReadOnlyList<string> Logs);
public sealed record ContextGenerationResponse(string OutputPath, IReadOnlyList<string> Files);
public sealed record ImportInstructionsResponse(Guid DocumentId, string ImportedPath, string ValidationPath, string RecipeId);
public sealed record RecipeListItemDto(string Id, string Name, string Description, string OutputPath);
public sealed record RunRecipeResponse(string RecipeId, string OutputPath, IReadOnlyList<string> Files);
public sealed record ChangeSetDto(IReadOnlyList<string> NewFiles, IReadOnlyList<string> ChangedFiles, IReadOnlyList<string> DeletedFiles, int UnchangedCount, int IgnoredCount);

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
public sealed record RunExportResponse(string ExportProfileId, string OutputPath, IReadOnlyList<string> Files);
public sealed record ExportPackResponse(string PackId, string OutputPath, bool Success);

public sealed record CleanupCandidateDto(string RelativePath, string Kind, bool SafeToDelete);
public sealed record CleanupPreviewDto(Guid ProjectId, IReadOnlyList<CleanupCandidateDto> Candidates, int SafeToDeleteCount, string RecommendedCommand);
public sealed record CleanupGeneratedRequest(bool DryRun = false, bool Force = false);
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
