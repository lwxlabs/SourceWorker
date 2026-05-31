namespace SourceWorker.Domain;

public sealed record Project(Guid Id, string Name, string RootPath, DateTimeOffset CreatedAt, DateTimeOffset? LastScannedAt = null);
public sealed record ScanRun(
    Guid Id,
    Guid ProjectId,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    ScanRunStatus Status,
    int FilesIndexed,
    int TodosFound,
    int TotalFilesSeen = 0,
    int IgnoredFileCount = 0,
    int ChangedFileCount = 0,
    int DetectedTechnologyCount = 0,
    string? ErrorMessage = null);
public sealed record IndexedFile(
    Guid Id,
    Guid ProjectId,
    Guid ScanRunId,
    string Path,
    string RelativePath,
    string Extension,
    long SizeBytes,
    DateTimeOffset LastModifiedUtc,
    string Sha256,
    IndexedFileKind Kind,
    bool WasIgnored,
    string? IgnoreReason,
    FileChangeKind ChangeKind);
public sealed record DetectedTechnology(Guid Id, Guid ProjectId, Guid ScanRunId, TechnologyKind Kind, string Evidence);
public sealed record GitSnapshot(Guid Id, Guid ProjectId, string? Branch, string? CommitSha, bool IsDirty, DateTimeOffset CapturedAt);
public sealed record TodoItem(Guid Id, Guid ProjectId, Guid ScanRunId, string RelativePath, int LineNumber, TodoKind Kind, string Text);
public sealed record ContextFile(Guid Id, Guid ProjectId, string RelativePath, string Purpose, DateTimeOffset GeneratedAt);
public sealed record PromptRecipe(Guid Id, string Name, string Slug, string Description, bool IsBuiltIn);
public sealed record WorkerJob(
    Guid Id,
    Guid? ProjectId,
    WorkerJobType Type,
    WorkerJobStatus Status,
    int ProgressPercent,
    string? Message,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt = null,
    DateTimeOffset? CompletedAt = null,
    string? CurrentStep = null,
    string? ErrorMessage = null,
    IReadOnlyList<string>? Logs = null);
public sealed record WorkerJobStep(Guid Id, Guid JobId, string Name, WorkerJobStatus Status, int ProgressPercent, string? Message, DateTimeOffset CreatedAt, DateTimeOffset? CompletedAt = null);

public enum ScanRunStatus { Queued, Running, Completed, Failed }
public enum IndexedFileKind { Source, Solution, Project, Markdown, Config, Docker, Workflow, Other }
public enum FileChangeKind { New, Changed, Deleted, Unchanged, Unknown }
public enum TechnologyKind { DotNet, AspNetCore, Blazor, Maui, Docker, GitHubActions }
public enum TodoKind { Todo, Fixme, Review }
public enum WorkerJobType { ProjectScan, ContextGeneration }
public enum WorkerJobStatus { Queued, Running, Completed, Failed, Cancelled }

public sealed record SolutionFile(string Name, string RelativePath);
public sealed record ProjectFile(string Name, string RelativePath, string Sdk, IReadOnlyList<string> ProjectReferences, bool IsTestProject);
public sealed record FolderRole(string RelativePath, string Role, string Description);

public sealed record ProjectInstructionDocument(
    Guid Id,
    Guid ProjectId,
    string SourceFilePath,
    string Title,
    DateTimeOffset ImportedAt,
    IReadOnlyList<ProjectInstructionSection> Sections,
    IReadOnlyList<ProjectInstructionSourcePath> SourcePaths,
    IReadOnlyList<ProjectInstructionRule> Rules,
    IReadOnlyList<ProjectInstructionTodo> Todos,
    IReadOnlyList<ProjectInstructionSkipItem> SkipItems,
    IReadOnlyList<ProjectInstructionVerificationItem> VerificationItems);

public sealed record ProjectInstructionSection(string Title, string NormalizedTitle, int Level, IReadOnlyList<ProjectInstructionItem> Items, string RawContent);
public sealed record ProjectInstructionItem(string Text, bool IsCheckbox, bool? IsChecked, int IndentLevel, int LineNumber);
public sealed record ProjectInstructionSourcePath(string OriginalPath, string NormalizedPath, bool Exists, SourcePathType Type, bool IsInsideProject, string? WarningMessage);
public sealed record ProjectInstructionRule(string Text, string SourceSection);
public sealed record ProjectInstructionTodo(string Text, bool IsCompleted, string SourceSection);
public sealed record ProjectInstructionSkipItem(string Text, string SourceSection);
public sealed record ProjectInstructionVerificationItem(string Text, bool IsCompleted, string SourceSection);

public sealed record ProjectRecipe(
    string Id,
    string Name,
    string Description,
    string Language,
    string OutputPath,
    string TargetPlatform,
    IReadOnlyList<string> IncludedSections,
    IReadOnlyList<string> SourceScopes,
    IReadOnlyList<string> Rules,
    IReadOnlyList<string> SkipItems,
    IReadOnlyList<string> VerificationChecklist,
    IReadOnlyList<string> GeneratedFiles,
    bool MermaidEnabled,
    bool RequireClassAndMethodNames,
    bool RequireSourceReferences,
    IReadOnlyDictionary<string, IReadOnlyList<string>> ExtraSections);

public sealed record SourcePathValidationReport(Guid ProjectId, IReadOnlyList<ProjectInstructionSourcePath> Paths, IReadOnlyList<string> Warnings);

public sealed record DoctorReport(
    string ProjectRoot,
    bool ProjectRootFound,
    bool GitRepoFound,
    IReadOnlyList<string> SolutionFiles,
    IReadOnlyList<string> ProjectFiles,
    IReadOnlyList<string> TestProjects,
    bool ContextFolderExists,
    bool ImportedInstructionsFound,
    IReadOnlyList<string> Recipes,
    IReadOnlyList<ProjectInstructionSourcePath> MissingSourcePaths,
    bool IsContextStale,
    DateTimeOffset? LastScanTime,
    IReadOnlyList<string> Warnings);

public enum SourcePathType { File, Folder, Unknown }
public enum GeneratedArtifactType { ContextFile, RecipeFile, BackendDocs, InstructionFile, ValidationFile, Other }
public enum GeneratedArtifactStatus { Generated, ManuallyModified, Stale, Missing }

public sealed record GeneratedArtifact(
    Guid Id,
    Guid ProjectId,
    Guid? ScanRunId,
    string? RecipeId,
    string RelativePath,
    string AbsolutePath,
    GeneratedArtifactType Type,
    string ContentHash,
    DateTimeOffset GeneratedUtc,
    string SourceInputHash,
    GeneratedArtifactStatus Status,
    string GeneratorName,
    string? Notes);

public sealed record ContextFreshnessReport(
    Guid ProjectId,
    bool HasStaleContext,
    IReadOnlyList<string> StaleContextFiles,
    IReadOnlyList<string> MissingGeneratedFiles,
    IReadOnlyList<string> ManuallyModifiedFiles,
    IReadOnlyList<string> FilesChangedSinceLastGeneration,
    int FreshnessScore,
    IReadOnlyList<string> RecommendedActions);

public sealed record ChangeSetReport(
    Guid ProjectId,
    Guid? ScanRunId,
    IReadOnlyList<string> NewFiles,
    IReadOnlyList<string> ChangedFiles,
    IReadOnlyList<string> DeletedFiles,
    int UnchangedCount,
    int IgnoredCount);
