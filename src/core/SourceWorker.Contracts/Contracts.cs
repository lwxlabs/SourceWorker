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
