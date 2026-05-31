using LwX.Flow;
using SourceWorker.Contracts;
using SourceWorker.Domain;

namespace SourceWorker.Application;

public sealed record AddProjectCommand(string Path, string? Name = null) : IRequest<ProjectDto>;
public sealed record GetProjectsQuery : IRequest<IReadOnlyList<ProjectDto>>;
public sealed record StartProjectScanCommand(Guid ProjectId) : IRequest<StartScanResponse>;
public sealed record GetJobStatusQuery(Guid JobId) : IRequest<JobStatusDto?>;
public sealed record GenerateBasicContextCommand(Guid ProjectId) : IRequest<ContextGenerationResponse>;

public sealed class AddProjectCommandHandler(IProjectRepository projects) : IRequestHandler<AddProjectCommand, ProjectDto>
{
    public async Task<ProjectDto> Handle(AddProjectCommand request, CancellationToken cancellationToken)
    {
        var root = Path.GetFullPath(request.Path);
        if (!Directory.Exists(root)) throw new DirectoryNotFoundException(root);
        var existing = await projects.GetByPathAsync(root, cancellationToken);
        if (existing is not null) return existing.ToDto();
        var name = string.IsNullOrWhiteSpace(request.Name) ? new DirectoryInfo(root).Name : request.Name.Trim();
        return (await projects.AddAsync(new Project(Guid.NewGuid(), name, root, DateTimeOffset.UtcNow), cancellationToken)).ToDto();
    }
}

public sealed class GetProjectsQueryHandler(IProjectRepository projects) : IRequestHandler<GetProjectsQuery, IReadOnlyList<ProjectDto>>
{
    public async Task<IReadOnlyList<ProjectDto>> Handle(GetProjectsQuery request, CancellationToken cancellationToken)
        => (await projects.ListAsync(cancellationToken)).Select(ProjectMappings.ToDto).ToArray();
}

public sealed class StartProjectScanCommandHandler(IProjectRepository projects, IJobRepository jobs, ILocalJobQueue queue) : IRequestHandler<StartProjectScanCommand, StartScanResponse>
{
    public async Task<StartScanResponse> Handle(StartProjectScanCommand request, CancellationToken cancellationToken)
    {
        _ = await projects.GetAsync(request.ProjectId, cancellationToken) ?? throw new InvalidOperationException($"Project '{request.ProjectId}' was not found.");
        var job = new WorkerJob(Guid.NewGuid(), request.ProjectId, WorkerJobType.ProjectScan, WorkerJobStatus.Queued, 0, "Queued", DateTimeOffset.UtcNow);
        await jobs.AddAsync(job, cancellationToken);
        await queue.EnqueueAsync(job.Id, cancellationToken);
        return new StartScanResponse(job.Id);
    }
}

public sealed class GetJobStatusQueryHandler(IJobRepository jobs) : IRequestHandler<GetJobStatusQuery, JobStatusDto?>
{
    public async Task<JobStatusDto?> Handle(GetJobStatusQuery request, CancellationToken cancellationToken)
        => (await jobs.GetAsync(request.JobId, cancellationToken))?.ToDto();
}

public sealed class GenerateBasicContextCommandHandler(IProjectRepository projects, IProjectScanner scanner, IFileIndexRepository fileIndex, IContextWriter contextWriter, IRecipeRunner recipeRunner)
    : IRequestHandler<GenerateBasicContextCommand, ContextGenerationResponse>
{
    public async Task<ContextGenerationResponse> Handle(GenerateBasicContextCommand request, CancellationToken cancellationToken)
    {
        var project = await projects.GetAsync(request.ProjectId, cancellationToken) ?? throw new InvalidOperationException($"Project '{request.ProjectId}' was not found.");
        var scan = await scanner.ScanAsync(project, cancellationToken);
        await fileIndex.ReplaceForScanAsync(scan.ScanRun.Id, scan.Files, scan.Todos, scan.Technologies, cancellationToken);
        var written = await contextWriter.WriteBasicContextAsync(scan, cancellationToken);
        await recipeRunner.RunBackendDocsCompactAsync(scan, cancellationToken);
        await projects.UpdateAsync(project with { LastScannedAt = scan.ScanRun.CompletedAt }, cancellationToken);
        return new ContextGenerationResponse(written.OutputPath, written.Files);
    }
}

public static class ProjectMappings
{
    public static ProjectDto ToDto(this Project project) => new(project.Id, project.Name, project.RootPath, project.CreatedAt, project.LastScannedAt);
    public static JobStatusDto ToDto(this WorkerJob job) => new(job.Id, job.ProjectId, job.Type, job.Status, job.ProgressPercent, job.Message, job.CurrentStep, job.ErrorMessage, job.Logs ?? []);
}
