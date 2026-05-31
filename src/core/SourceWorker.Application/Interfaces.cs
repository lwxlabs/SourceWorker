using SourceWorker.Domain;

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
    Task<ScanRun> UpdateAsync(ScanRun scanRun, CancellationToken cancellationToken);
}

public interface IFileIndexRepository
{
    Task ReplaceForScanAsync(Guid scanRunId, IReadOnlyList<IndexedFile> files, IReadOnlyList<TodoItem> todos, IReadOnlyList<DetectedTechnology> technologies, CancellationToken cancellationToken);
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
}

public interface ILocalJobQueue
{
    ValueTask EnqueueAsync(Guid jobId, CancellationToken cancellationToken);
    ValueTask<Guid> DequeueAsync(CancellationToken cancellationToken);
}

public sealed record ProjectScanResult(Project Project, ScanRun ScanRun, IReadOnlyList<IndexedFile> Files, IReadOnlyList<TodoItem> Todos, IReadOnlyList<DetectedTechnology> Technologies, GitSnapshot Git);
public sealed record ContextWriteResult(string OutputPath, IReadOnlyList<string> Files);
