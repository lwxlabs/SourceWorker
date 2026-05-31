using SourceWorker.Domain;

namespace SourceWorker.Contracts;

public sealed record ProjectDto(Guid Id, string Name, string RootPath, DateTimeOffset CreatedAt, DateTimeOffset? LastScannedAt);
public sealed record AddProjectRequest(string Path, string? Name);
public sealed record CreateProjectResponse(ProjectDto Project);
public sealed record StartScanResponse(Guid JobId);
public sealed record JobStatusDto(Guid Id, Guid? ProjectId, WorkerJobType Type, WorkerJobStatus Status, int ProgressPercent, string? Message);
public sealed record ContextGenerationResponse(string OutputPath, IReadOnlyList<string> Files);
