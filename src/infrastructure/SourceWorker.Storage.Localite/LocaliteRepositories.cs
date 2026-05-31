using System.Collections.Concurrent;
using Localite;
using Microsoft.Extensions.DependencyInjection;
using SourceWorker.Application;
using SourceWorker.Domain;

namespace SourceWorker.Storage.Localite;

public static class LocaliteStorage
{
    public static IServiceCollection AddSourceWorkerLocaliteStorage(this IServiceCollection services)
    {
        services.AddLocalite().UseCache(options =>
        {
            options.ApplicationId = "SourceWorker";
            options.DbName = "sourceworker.db3";
        });

        services.AddSingleton<LocaliteJsonStore>();
        services.AddSingleton<IProjectRepository, LocaliteProjectRepository>();
        services.AddSingleton<IScanRunRepository, LocaliteScanRunRepository>();
        services.AddSingleton<IFileIndexRepository, LocaliteFileIndexRepository>();
        services.AddSingleton<IIndexedFileRepository, LocaliteIndexedFileRepository>();
        services.AddSingleton<IGeneratedArtifactRepository, LocaliteGeneratedArtifactRepository>();
        services.AddSingleton<IJobRepository, LocaliteJobRepository>();
        services.AddSingleton<IProjectInstructionRepository, LocaliteProjectInstructionRepository>();
        return services;
    }
}

public sealed class LocaliteJsonStore(ILocaliteStore store)
{
    private static readonly TimeSpan LongTtl = TimeSpan.FromDays(3650);

    public Task<IReadOnlyList<T>> GetListAsync<T>(string key, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<T>>(store.Get<List<T>>(key, options: null, decrypt: false) ?? []);
    }

    public Task SetListAsync<T>(string key, IReadOnlyList<T> values, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        store.Add(key, values.ToList(), LongTtl, secure: false);
        return Task.CompletedTask;
    }
}

public sealed class LocaliteProjectRepository(LocaliteJsonStore store) : IProjectRepository
{
    private const string Key = "projects";

    public async Task<Project> AddAsync(Project project, CancellationToken cancellationToken)
    {
        var projects = (await store.GetListAsync<Project>(Key, cancellationToken)).ToList();
        projects.Add(project);
        await store.SetListAsync(Key, projects, cancellationToken);
        return project;
    }

    public async Task<Project?> GetAsync(Guid id, CancellationToken cancellationToken)
        => (await store.GetListAsync<Project>(Key, cancellationToken)).FirstOrDefault(p => p.Id == id);

    public async Task<Project?> GetByPathAsync(string rootPath, CancellationToken cancellationToken)
        => (await store.GetListAsync<Project>(Key, cancellationToken)).FirstOrDefault(p => Path.GetFullPath(p.RootPath).Equals(Path.GetFullPath(rootPath), StringComparison.OrdinalIgnoreCase));

    public Task<IReadOnlyList<Project>> ListAsync(CancellationToken cancellationToken)
        => store.GetListAsync<Project>(Key, cancellationToken);

    public async Task<Project> UpdateAsync(Project project, CancellationToken cancellationToken)
    {
        var projects = (await store.GetListAsync<Project>(Key, cancellationToken)).Where(p => p.Id != project.Id).Append(project).OrderBy(p => p.Name).ToArray();
        await store.SetListAsync(Key, projects, cancellationToken);
        return project;
    }
}

public sealed class LocaliteScanRunRepository(LocaliteJsonStore store) : IScanRunRepository
{
    private const string Key = "scan-runs";

    public async Task<ScanRun> AddAsync(ScanRun scanRun, CancellationToken cancellationToken)
    {
        var runs = (await store.GetListAsync<ScanRun>(Key, cancellationToken)).ToList();
        runs.Add(scanRun);
        await store.SetListAsync(Key, runs, cancellationToken);
        return scanRun;
    }

    public async Task<ScanRun?> GetAsync(Guid id, CancellationToken cancellationToken)
        => (await store.GetListAsync<ScanRun>(Key, cancellationToken)).FirstOrDefault(run => run.Id == id);

    public async Task<IReadOnlyList<ScanRun>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken)
        => (await store.GetListAsync<ScanRun>(Key, cancellationToken))
            .Where(run => run.ProjectId == projectId)
            .OrderByDescending(run => run.StartedAt)
            .ToArray();

    public async Task<ScanRun?> GetLatestAsync(Guid projectId, CancellationToken cancellationToken)
        => (await GetByProjectAsync(projectId, cancellationToken)).FirstOrDefault();

    public async Task<ScanRun> UpdateAsync(ScanRun scanRun, CancellationToken cancellationToken)
    {
        var runs = (await store.GetListAsync<ScanRun>(Key, cancellationToken)).Where(run => run.Id != scanRun.Id).Append(scanRun).ToArray();
        await store.SetListAsync(Key, runs, cancellationToken);
        return scanRun;
    }
}

public sealed class LocaliteIndexedFileRepository(LocaliteJsonStore store) : IIndexedFileRepository
{
    public async Task SaveForScanAsync(Guid projectId, Guid scanRunId, IReadOnlyList<IndexedFile> files, CancellationToken cancellationToken)
    {
        await store.SetListAsync(Key(scanRunId), files, cancellationToken);
        var scans = (await store.GetListAsync<Guid>(ProjectKey(projectId), cancellationToken)).Where(id => id != scanRunId).Append(scanRunId).ToArray();
        await store.SetListAsync(ProjectKey(projectId), scans, cancellationToken);
    }

    public Task<IReadOnlyList<IndexedFile>> GetByScanRunAsync(Guid scanRunId, CancellationToken cancellationToken)
        => store.GetListAsync<IndexedFile>(Key(scanRunId), cancellationToken);

    public async Task<IReadOnlyList<IndexedFile>> GetLatestByProjectAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var scanIds = await store.GetListAsync<Guid>(ProjectKey(projectId), cancellationToken);
        var latest = scanIds.LastOrDefault();
        return latest == Guid.Empty ? [] : await GetByScanRunAsync(latest, cancellationToken);
    }

    private static string Key(Guid scanRunId) => $"indexed-files:{scanRunId}";
    private static string ProjectKey(Guid projectId) => $"indexed-files:project:{projectId}";
}

public sealed class LocaliteGeneratedArtifactRepository(LocaliteJsonStore store) : IGeneratedArtifactRepository
{
    public async Task<GeneratedArtifact> UpsertAsync(GeneratedArtifact artifact, CancellationToken cancellationToken)
    {
        var artifacts = (await store.GetListAsync<GeneratedArtifact>(Key(artifact.ProjectId), cancellationToken))
            .Where(existing => !existing.RelativePath.Equals(artifact.RelativePath, StringComparison.OrdinalIgnoreCase))
            .Append(artifact)
            .OrderBy(a => a.RelativePath)
            .ToArray();
        await store.SetListAsync(Key(artifact.ProjectId), artifacts, cancellationToken);
        return artifact;
    }

    public Task<IReadOnlyList<GeneratedArtifact>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken)
        => store.GetListAsync<GeneratedArtifact>(Key(projectId), cancellationToken);

    public async Task<GeneratedArtifact?> GetByPathAsync(Guid projectId, string relativePath, CancellationToken cancellationToken)
        => (await GetByProjectAsync(projectId, cancellationToken)).FirstOrDefault(a => a.RelativePath.Equals(relativePath, StringComparison.OrdinalIgnoreCase));

    private static string Key(Guid projectId) => $"generated-artifacts:{projectId}";
}

public sealed class LocaliteFileIndexRepository(LocaliteJsonStore store) : IFileIndexRepository
{
    public Task ReplaceForScanAsync(Guid scanRunId, IReadOnlyList<IndexedFile> files, IReadOnlyList<TodoItem> todos, IReadOnlyList<DetectedTechnology> technologies, CancellationToken cancellationToken)
        => Task.WhenAll(
            store.SetListAsync($"files:{scanRunId}", files, cancellationToken),
            store.SetListAsync($"todos:{scanRunId}", todos, cancellationToken),
            store.SetListAsync($"technologies:{scanRunId}", technologies, cancellationToken));
}

public sealed class LocaliteJobRepository(LocaliteJsonStore store) : IJobRepository
{
    private const string JobsKey = "jobs";
    private static readonly ConcurrentDictionary<Guid, List<WorkerJobStep>> Steps = [];

    public async Task<WorkerJob> AddAsync(WorkerJob job, CancellationToken cancellationToken)
    {
        var jobs = (await store.GetListAsync<WorkerJob>(JobsKey, cancellationToken)).ToList();
        jobs.Add(job);
        await store.SetListAsync(JobsKey, jobs, cancellationToken);
        return job;
    }

    public async Task<WorkerJob?> GetAsync(Guid id, CancellationToken cancellationToken)
        => (await store.GetListAsync<WorkerJob>(JobsKey, cancellationToken)).FirstOrDefault(job => job.Id == id);

    public Task<IReadOnlyList<WorkerJob>> ListAsync(CancellationToken cancellationToken)
        => store.GetListAsync<WorkerJob>(JobsKey, cancellationToken);

    public async Task<WorkerJob> UpdateAsync(WorkerJob job, CancellationToken cancellationToken)
    {
        var jobs = (await store.GetListAsync<WorkerJob>(JobsKey, cancellationToken)).Where(existing => existing.Id != job.Id).Append(job).ToArray();
        await store.SetListAsync(JobsKey, jobs, cancellationToken);
        return job;
    }

    public Task AddStepAsync(WorkerJobStep step, CancellationToken cancellationToken)
    {
        Steps.AddOrUpdate(step.JobId, _ => [step], (_, steps) =>
        {
            steps.Add(step);
            return steps;
        });
        return Task.CompletedTask;
    }
}

public sealed class LocaliteProjectInstructionRepository(LocaliteJsonStore store) : IProjectInstructionRepository
{
    public async Task<ProjectInstructionDocument?> GetLatestAsync(Guid projectId, CancellationToken cancellationToken)
        => (await store.GetListAsync<ProjectInstructionDocument>(Key(projectId), cancellationToken))
            .OrderByDescending(document => document.ImportedAt)
            .FirstOrDefault();

    public async Task<ProjectInstructionDocument> SaveAsync(ProjectInstructionDocument document, CancellationToken cancellationToken)
    {
        var documents = (await store.GetListAsync<ProjectInstructionDocument>(Key(document.ProjectId), cancellationToken))
            .Where(existing => existing.Id != document.Id)
            .Append(document)
            .OrderByDescending(existing => existing.ImportedAt)
            .ToArray();
        await store.SetListAsync(Key(document.ProjectId), documents, cancellationToken);
        return document;
    }

    private static string Key(Guid projectId) => $"instructions:{projectId}";
}
