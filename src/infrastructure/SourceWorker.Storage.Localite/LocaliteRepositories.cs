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
        services.AddSingleton<IJobRepository, LocaliteJobRepository>();
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

    public async Task<ScanRun> UpdateAsync(ScanRun scanRun, CancellationToken cancellationToken)
    {
        var runs = (await store.GetListAsync<ScanRun>(Key, cancellationToken)).Where(run => run.Id != scanRun.Id).Append(scanRun).ToArray();
        await store.SetListAsync(Key, runs, cancellationToken);
        return scanRun;
    }
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
