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
        var job = await jobs.GetAsync(jobId, cancellationToken);
        if (job?.ProjectId is null) return;

        try
        {
            await jobs.UpdateAsync(job with { Status = WorkerJobStatus.Running, ProgressPercent = 10, StartedAt = DateTimeOffset.UtcNow, Message = "Scanning project" }, cancellationToken);
            var project = await projects.GetAsync(job.ProjectId.Value, cancellationToken) ?? throw new InvalidOperationException($"Project '{job.ProjectId}' was not found.");
            var scan = await scanner.ScanAsync(project, cancellationToken);
            await fileIndex.ReplaceForScanAsync(scan.ScanRun.Id, scan.Files, scan.Todos, scan.Technologies, cancellationToken);
            await jobs.UpdateAsync(job with { Status = WorkerJobStatus.Running, ProgressPercent = 75, StartedAt = job.StartedAt ?? DateTimeOffset.UtcNow, Message = "Writing context" }, cancellationToken);
            await contextWriter.WriteBasicContextAsync(scan, cancellationToken);
            await projects.UpdateAsync(project with { LastScannedAt = scan.ScanRun.CompletedAt }, cancellationToken);
            await jobs.UpdateAsync(job with { Status = WorkerJobStatus.Completed, ProgressPercent = 100, CompletedAt = DateTimeOffset.UtcNow, Message = "Completed" }, cancellationToken);
        }
        catch (Exception ex)
        {
            await jobs.UpdateAsync(job with { Status = WorkerJobStatus.Failed, Message = ex.Message, CompletedAt = DateTimeOffset.UtcNow }, CancellationToken.None);
        }
    }
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
