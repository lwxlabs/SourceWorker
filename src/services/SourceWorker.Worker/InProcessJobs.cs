using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SourceWorker.Application;
using SourceWorker.Domain;

namespace SourceWorker.Worker;

public sealed class InProcessLocalJobQueue : ILocalJobQueue
{
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>();

    public ValueTask EnqueueAsync(Guid jobId, CancellationToken cancellationToken) => _channel.Writer.WriteAsync(jobId, cancellationToken);
    public ValueTask<Guid> DequeueAsync(CancellationToken cancellationToken) => _channel.Reader.ReadAsync(cancellationToken);
}

public sealed class ProjectScanWorker(IServiceProvider serviceProvider) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = serviceProvider.CreateScope();
            var queue = scope.ServiceProvider.GetRequiredService<ILocalJobQueue>();
            var jobId = await queue.DequeueAsync(stoppingToken);
            await ExecuteJobAsync(jobId, scope.ServiceProvider, stoppingToken);
        }
    }

    public static async Task ExecuteJobAsync(Guid jobId, IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var jobs = serviceProvider.GetRequiredService<IJobRepository>();
        var projects = serviceProvider.GetRequiredService<IProjectRepository>();
        var scanner = serviceProvider.GetRequiredService<IProjectScanner>();
        var fileIndex = serviceProvider.GetRequiredService<IFileIndexRepository>();
        var contextWriter = serviceProvider.GetRequiredService<IContextWriter>();
        var recipeRunner = serviceProvider.GetRequiredService<IRecipeRunner>();
        var job = await jobs.GetAsync(jobId, cancellationToken);
        if (job?.ProjectId is null) return;

        try
        {
            job = await Update(job, jobs, WorkerJobStatus.Running, 5, "Reading project folder", null, cancellationToken);
            var projectId = job.ProjectId.GetValueOrDefault();
            var project = await projects.GetAsync(projectId, cancellationToken) ?? throw new InvalidOperationException($"Project '{job.ProjectId}' was not found.");
            job = await Update(job, jobs, WorkerJobStatus.Running, 15, "Applying ignore rules", null, cancellationToken);
            job = await Update(job, jobs, WorkerJobStatus.Running, 25, "Detecting solution files", null, cancellationToken);
            var scan = await scanner.ScanAsync(project, cancellationToken);
            job = await Update(job, jobs, WorkerJobStatus.Running, 55, "Indexing files", $"{scan.Files.Count} files indexed", cancellationToken);
            await fileIndex.ReplaceForScanAsync(scan.ScanRun.Id, scan.Files, scan.Todos, scan.Technologies, cancellationToken);
            job = await Update(job, jobs, WorkerJobStatus.Running, 70, "Extracting TODOs", $"{scan.Todos.Count} TODO/FIXME/REVIEW items found", cancellationToken);
            job = await Update(job, jobs, WorkerJobStatus.Running, 80, "Detecting Git state", scan.Git.Branch ?? "No Git branch detected", cancellationToken);
            job = await Update(job, jobs, WorkerJobStatus.Running, 90, "Writing context files", null, cancellationToken);
            await contextWriter.WriteBasicContextAsync(scan, cancellationToken);
            await recipeRunner.RunBackendDocsCompactAsync(scan, cancellationToken);
            await projects.UpdateAsync(project with { LastScannedAt = scan.ScanRun.CompletedAt }, cancellationToken);
            await Update(job, jobs, WorkerJobStatus.Completed, 100, "Completed", "Scan completed", cancellationToken);
        }
        catch (Exception ex)
        {
            await jobs.UpdateAsync(job with { Status = WorkerJobStatus.Failed, Message = ex.Message, CurrentStep = "Failed", ErrorMessage = ex.Message, CompletedAt = DateTimeOffset.UtcNow, Logs = AppendLog(job.Logs, $"Failed: {ex.Message}") }, CancellationToken.None);
        }
    }

    private static async Task<WorkerJob> Update(WorkerJob job, IJobRepository jobs, WorkerJobStatus status, int progress, string step, string? log, CancellationToken cancellationToken)
    {
        var updated = job with
        {
            Status = status,
            ProgressPercent = progress,
            StartedAt = job.StartedAt ?? DateTimeOffset.UtcNow,
            CompletedAt = status is WorkerJobStatus.Completed or WorkerJobStatus.Failed or WorkerJobStatus.Cancelled ? DateTimeOffset.UtcNow : job.CompletedAt,
            CurrentStep = step,
            Message = step,
            Logs = AppendLog(job.Logs, log ?? step)
        };

        return await jobs.UpdateAsync(updated, cancellationToken);
    }

    private static IReadOnlyList<string> AppendLog(IReadOnlyList<string>? logs, string value)
        => (logs ?? []).Append($"{DateTimeOffset.UtcNow:O} {value}").ToArray();
}

public static class WorkerServices
{
    public static IServiceCollection AddSourceWorkerJobs(this IServiceCollection services, bool addHostedWorker = true)
    {
        services.AddSingleton<ILocalJobQueue, InProcessLocalJobQueue>();
        if (addHostedWorker) services.AddHostedService<ProjectScanWorker>();
        return services;
    }
}
