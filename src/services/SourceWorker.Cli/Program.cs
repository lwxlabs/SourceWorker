using LwX.Flow;
using Microsoft.Extensions.DependencyInjection;
using SourceWorker.Application;
using SourceWorker.Infrastructure;
using SourceWorker.Worker;

var services = new ServiceCollection()
    .AddSourceWorkerApplication()
    .AddSourceWorkerInfrastructure()
    .AddSourceWorkerJobs(addHostedWorker: false)
    .BuildServiceProvider();

var flow = services.GetRequiredService<IRequestFlow>();

if (args.Length == 0)
{
    PrintUsage();
    return 1;
}

switch (args[0].ToLowerInvariant())
{
    case "scan" when args.Length >= 2:
    {
        var project = await flow.Send(new AddProjectCommand(args[1]));
        var response = await flow.Send(new GenerateBasicContextCommand(project.Id));
        Console.WriteLine($"Scanned {project.Name}");
        Console.WriteLine($"Context: {response.OutputPath}");
        return 0;
    }
    case "status":
    {
        var jobs = services.GetRequiredService<IJobRepository>();
        foreach (var job in await jobs.ListAsync(CancellationToken.None))
        {
            Console.WriteLine($"{job.Id} {job.Type} {job.Status} {job.ProgressPercent}% {job.Message}");
        }

        return 0;
    }
    case "handoff" when args.Length >= 2:
    {
        var project = await flow.Send(new AddProjectCommand(args[1]));
        var response = await flow.Send(new GenerateBasicContextCommand(project.Id));
        Console.WriteLine(Path.Combine(response.OutputPath, "ai-handoff.md"));
        return 0;
    }
    default:
        PrintUsage();
        return 1;
}

static void PrintUsage()
{
    Console.WriteLine("sourceworker scan <path>");
    Console.WriteLine("sourceworker status");
    Console.WriteLine("sourceworker handoff <path>");
}
