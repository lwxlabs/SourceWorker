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
public enum GeneratedArtifactType { ContextFile, RecipeFile, BackendDocs, InstructionFile, ValidationFile, Other, InternalContextFile, InternalGeneratedDoc, ExternalExportedDoc, ContextPackManifest, ExportProfile, DiagramFile, InstructionImportFile }
public enum GeneratedArtifactStatus { Generated, ManuallyModified, Stale, Missing, Orphaned, Deprecated }
public enum OutputDestinationType { InternalContextDocs, ExternalMarkdownFolder, YouTrackKnowledgeBaseLater, GitHubWikiLater, ObsidianVaultLater, ZipExportLater }

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

public sealed record OutputDestination(string Id, OutputDestinationType Type, string Path, bool Primary, string PublishMode = "mirror");
public sealed record OutputProfile(string Id, string RecipeId, IReadOnlyList<OutputDestination> Destinations, IReadOnlyList<string> GeneratedFiles);
public sealed record OutputRoute(string RecipeId, OutputDestination Destination, IReadOnlyList<string> Files);
public sealed record GeneratedOutputSet(string RecipeId, IReadOnlyList<OutputDestination> Destinations, IReadOnlyList<GeneratedOutputFile> Files, IReadOnlyList<string> Warnings);
public sealed record GeneratedOutputFile(string RelativePath, string DestinationId, GeneratedArtifactType ArtifactType, GeneratedArtifactStatus Status, bool Exists);
public sealed record ExportProfile(string Id, string RecipeId, string InternalSourcePath, string ExternalOutputPath, string TargetPlatform, string PublishMode, IReadOnlyList<string> GeneratedFiles, DateTimeOffset? LastExportUtc, Guid? LastExportScanRunId, IReadOnlyList<Guid> ArtifactIds, IReadOnlyList<string> Warnings, string CleanupBehavior);
public sealed record ExportRun(string Id, string ExportProfileId, Guid ProjectId, DateTimeOffset StartedUtc, DateTimeOffset FinishedUtc, IReadOnlyList<string> ExportedFiles, IReadOnlyList<string> Warnings);
public sealed record ContextPack(string Id, string Name, string Description, string Audience, string Purpose, IReadOnlyList<ContextPackItem> Items, IReadOnlyList<string> IncludedSelectors, string MaxRecommendedUse, int TokenEstimate, string FreshnessStatus, Guid? SourceScanRunId, DateTimeOffset GeneratedUtc);
public sealed record ContextPackItem(string RelativePath, string Kind, bool Required);
public sealed record ContextPackManifest(ContextPack Pack, string RelativePath, IReadOnlyList<string> MissingFiles, bool IsValid);

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

public sealed record LocalStorageSchemaInfo(string Provider, string Mode, string StoragePath, int SchemaVersion, DateTimeOffset CreatedUtc, DateTimeOffset LastMigratedUtc, string AppStorageFormatVersion);
public sealed record StorageInfo(LocalStorageSchemaInfo Schema, int ProjectCount, int ScanRunCount, int IndexedFileCount, int GeneratedArtifactCount, int JobCount, int InstructionCount, int RecipeCount, bool RepositoryHealth, int CodeSymbolCount = 0, int CodeFlowCount = 0, int RouteCount = 0, int ServiceRegistrationCount = 0, int ConfigurationUsageCount = 0, int TestInfoCount = 0);

public sealed record ArtifactLifecycleReport(
    Guid ProjectId,
    IReadOnlyList<GeneratedArtifact> Artifacts,
    IReadOnlyList<string> MissingArtifacts,
    IReadOnlyList<string> ManuallyModifiedArtifacts,
    IReadOnlyList<string> StaleArtifacts,
    IReadOnlyList<string> OrphanedArtifacts,
    IReadOnlyList<string> DeprecatedArtifacts,
    bool CleanupRecommended);

public sealed record CleanupPreviewReport(Guid ProjectId, IReadOnlyList<string> SafeDeleteFiles, IReadOnlyList<string> MissingTrackedArtifacts, IReadOnlyList<string> StaleTrackedArtifacts, IReadOnlyList<string> Warnings);
public sealed record CleanupResult(Guid ProjectId, bool DryRun, IReadOnlyList<string> DeletedFiles, IReadOnlyList<string> SkippedFiles, IReadOnlyList<string> Warnings);

public sealed record CodeSymbol(Guid Id, Guid ProjectId, Guid ScanRunId, string FilePath, string RelativePath, string? Namespace, string Name, string FullName, CodeSymbolKind Kind, string Accessibility, string? ParentSymbolName, string? ReturnType, IReadOnlyList<string> Parameters, int LineNumber, IReadOnlyList<string> Attributes, bool IsStatic, bool IsAsync);
public sealed record CodeFileSummary(Guid Id, Guid ProjectId, Guid ScanRunId, string RelativePath, int SymbolCount, int RouteCount, int ServiceRegistrationCount, int ConfigurationUsageCount);
public sealed record CodeProjectSummary(Guid Id, Guid ProjectId, Guid ScanRunId, int SymbolCount, int FlowCount, int RouteCount, int ServiceRegistrationCount, int ConfigurationUsageCount, int TestCount);
public sealed record CodeDependency(Guid Id, Guid ProjectId, Guid ScanRunId, string FromSymbol, string ToSymbol, string Kind, string RelativePath);
public sealed record CodeEntrypoint(Guid Id, Guid ProjectId, Guid ScanRunId, string Name, string RelativePath, string Kind, int LineNumber);
public sealed record CodeFlow(Guid Id, Guid ProjectId, Guid ScanRunId, string Name, CodeFlowType FlowType, string Trigger, IReadOnlyList<string> InvolvedFiles, IReadOnlyList<string> InvolvedSymbols, IReadOnlyList<CodeFlowStep> Steps, FlowConfidence Confidence, IReadOnlyList<string> Warnings);
public sealed record CodeFlowStep(int Order, string Text, string? SymbolName, string? RelativePath);
public sealed record RouteInfo(Guid Id, Guid ProjectId, Guid ScanRunId, string RelativePath, string HttpMethod, string RoutePattern, string? HandlerSymbol, int LineNumber);
public sealed record ConfigurationUsage(Guid Id, Guid ProjectId, Guid ScanRunId, string RelativePath, string KeyOrSection, string UsageKind, int LineNumber);
public sealed record ServiceRegistration(Guid Id, Guid ProjectId, Guid ScanRunId, string RelativePath, string RegistrationMethod, string ServiceType, string? ImplementationType, int LineNumber);
public sealed record BackgroundWorkerInfo(Guid Id, Guid ProjectId, Guid ScanRunId, string RelativePath, string ClassName, string BaseTypeOrInterface, int LineNumber);
public sealed record PublicApiEndpoint(Guid Id, Guid ProjectId, Guid ScanRunId, string RelativePath, string Method, string Route, string? Handler, int LineNumber);
public sealed record TestInfo(Guid Id, Guid ProjectId, Guid ScanRunId, string RelativePath, string ClassName, string MethodName, string TestFramework, int LineNumber);

public enum CodeSymbolKind { Class, Interface, Record, Struct, Enum, Method, Constructor, Property, Field }
public enum CodeFlowType { CliCommandFlow, LocalApiEndpointFlow, ScanFlow, InstructionImportFlow, RecipeRunFlow, DoctorFlow, ArtifactCleanupFlow, StorageInfoFlow, WorkerJobFlow, ContextGenerationFlow }
public enum FlowConfidence { High, Medium, Low }

public sealed record ContextBrowserNode(string Name, string RelativePath, string Kind, IReadOnlyList<ContextBrowserNode> Children);
public sealed record ContextBrowserFile(string RelativePath, string AbsolutePath, string Kind, long SizeBytes, DateTimeOffset LastModifiedUtc);
public sealed record ContextBrowserSummary(Guid ProjectId, IReadOnlyList<ContextBrowserFile> Files, IReadOnlyList<ProjectRecipe> Recipes, IReadOnlyList<CodeFlow> Flows, IReadOnlyList<RouteInfo> Routes, IReadOnlyList<TodoItem> Todos, IReadOnlyList<GeneratedArtifact> ProblemArtifacts);
public sealed record ContextBrowserQueryResult(string Query, IReadOnlyList<ContextBrowserSearchHit> Hits);
public sealed record ContextBrowserSearchHit(string RelativePath, int LineNumber, string Preview);
