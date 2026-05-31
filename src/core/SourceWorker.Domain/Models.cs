namespace SourceWorker.Domain;

public sealed record Project(Guid Id, string Name, string RootPath, DateTimeOffset CreatedAt, DateTimeOffset? LastScannedAt = null);
public sealed record ScanRun(Guid Id, Guid ProjectId, DateTimeOffset StartedAt, DateTimeOffset? CompletedAt, ScanRunStatus Status, int FilesIndexed, int TodosFound);
public sealed record IndexedFile(Guid Id, Guid ProjectId, Guid ScanRunId, string RelativePath, string Extension, long SizeBytes, IndexedFileKind Kind);
public sealed record DetectedTechnology(Guid Id, Guid ProjectId, Guid ScanRunId, TechnologyKind Kind, string Evidence);
public sealed record GitSnapshot(Guid Id, Guid ProjectId, string? Branch, string? CommitSha, bool IsDirty, DateTimeOffset CapturedAt);
public sealed record TodoItem(Guid Id, Guid ProjectId, Guid ScanRunId, string RelativePath, int LineNumber, TodoKind Kind, string Text);
public sealed record ContextFile(Guid Id, Guid ProjectId, string RelativePath, string Purpose, DateTimeOffset GeneratedAt);
public sealed record PromptRecipe(Guid Id, string Name, string Slug, string Description, bool IsBuiltIn);
public sealed record WorkerJob(Guid Id, Guid? ProjectId, WorkerJobType Type, WorkerJobStatus Status, int ProgressPercent, string? Message, DateTimeOffset CreatedAt, DateTimeOffset? StartedAt = null, DateTimeOffset? CompletedAt = null);
public sealed record WorkerJobStep(Guid Id, Guid JobId, string Name, WorkerJobStatus Status, int ProgressPercent, string? Message, DateTimeOffset CreatedAt, DateTimeOffset? CompletedAt = null);

public enum ScanRunStatus { Queued, Running, Completed, Failed }
public enum IndexedFileKind { Source, Solution, Project, Markdown, Docker, Workflow, Other }
public enum TechnologyKind { DotNet, AspNetCore, Blazor, Maui, Docker, GitHubActions }
public enum TodoKind { Todo, Fixme, Review }
public enum WorkerJobType { ProjectScan, ContextGeneration }
public enum WorkerJobStatus { Queued, Running, Completed, Failed }
