using System.Text.Json;
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
        var instructionPath = OptionValue(args, "--instructions");
        if (!string.IsNullOrWhiteSpace(instructionPath))
        {
            var import = await flow.Send(new ImportProjectInstructionsCommand(project.Id, instructionPath));
            Console.WriteLine($"Imported instructions: {import.RecipeId}");
        }

        var response = await flow.Send(new GenerateBasicContextCommand(project.Id));
        Console.WriteLine($"Scanned {project.Name}");
        Console.WriteLine($"Context: {response.OutputPath}");
        return 0;
    }
    case "import-instructions" when args.Length >= 3:
    {
        var project = await flow.Send(new AddProjectCommand(args[1]));
        var response = await flow.Send(new ImportProjectInstructionsCommand(project.Id, args[2]));
        Console.WriteLine($"Imported instructions: {response.ImportedPath}");
        Console.WriteLine($"Validation: {response.ValidationPath}");
        Console.WriteLine($"Recipe: {response.RecipeId}");
        return 0;
    }
    case "status":
    {
        var jobs = services.GetRequiredService<IJobRepository>();
        foreach (var job in await jobs.ListAsync(CancellationToken.None))
        {
            Console.WriteLine($"{job.Id} {job.Type} {job.Status} {job.ProgressPercent}% {job.CurrentStep ?? job.Message}");
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
    case "doctor" when args.Length >= 2:
    {
        var project = await flow.Send(new AddProjectCommand(args[1]));
        var report = await flow.Send(new DoctorProjectQuery(project.Id));
        if (args.Contains("--json", StringComparer.OrdinalIgnoreCase))
        {
            Console.WriteLine(JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        }
        else
        {
            Console.WriteLine($"Project root found: {report.ProjectRootFound}");
            Console.WriteLine($"Git repo found: {report.GitRepoFound}");
            Console.WriteLine($"Solutions: {report.SolutionFiles.Count}");
            Console.WriteLine($"Projects: {report.ProjectFiles.Count}");
            Console.WriteLine($"Tests: {report.TestProjects.Count}");
            Console.WriteLine($"Context folder exists: {report.ContextFolderExists}");
            Console.WriteLine($"Imported instructions found: {report.ImportedInstructionsFound}");
            Console.WriteLine($"Recipes: {report.Recipes.Count}");
            Console.WriteLine($"Missing source paths: {report.MissingSourcePaths.Count}");
            Console.WriteLine($"Stale context: {report.IsContextStale}");
            Console.WriteLine($"Last scan time: {report.LastScanTime:O}");
            foreach (var warning in report.Warnings) Console.WriteLine($"Warning: {warning}");
        }

        return 0;
    }
    case "recipes" when args.Length >= 2:
    {
        var project = await flow.Send(new AddProjectCommand(args[1]));
        foreach (var recipe in await flow.Send(new GetRecipesQuery(project.Id)))
        {
            Console.WriteLine($"{recipe.Id} - {recipe.Name} -> {recipe.OutputPath}");
        }

        return 0;
    }
    case "run-recipe" when args.Length >= 3:
    {
        var project = await flow.Send(new AddProjectCommand(args[1]));
        var response = await flow.Send(new RunRecipeCommand(project.Id, args[2], args.Contains("--force", StringComparer.OrdinalIgnoreCase)));
        Console.WriteLine($"Ran recipe: {response.RecipeId}");
        Console.WriteLine($"Output: {response.OutputPath}");
        foreach (var file in response.Files) Console.WriteLine(file);
        return 0;
    }
    case "changes" when args.Length >= 2:
    {
        var project = await flow.Send(new AddProjectCommand(args[1]));
        var changes = await flow.Send(new GetProjectChangesQuery(project.Id));
        if (args.Contains("--json", StringComparer.OrdinalIgnoreCase))
        {
            Console.WriteLine(JsonSerializer.Serialize(changes, new JsonSerializerOptions { WriteIndented = true }));
        }
        else
        {
            Console.WriteLine($"New files: {changes.NewFiles.Count}");
            foreach (var file in changes.NewFiles) Console.WriteLine($"  + {file}");
            Console.WriteLine($"Changed files: {changes.ChangedFiles.Count}");
            foreach (var file in changes.ChangedFiles) Console.WriteLine($"  * {file}");
            Console.WriteLine($"Deleted files: {changes.DeletedFiles.Count}");
            foreach (var file in changes.DeletedFiles) Console.WriteLine($"  - {file}");
            Console.WriteLine($"Unchanged count: {changes.UnchangedCount}");
            Console.WriteLine($"Ignored count: {changes.IgnoredCount}");
        }

        return 0;
    }
    case "context-status" when args.Length >= 2:
    {
        var project = await flow.Send(new AddProjectCommand(args[1]));
        var status = await flow.Send(new GetContextStatusQuery(project.Id));
        Console.WriteLine($"Freshness score: {status.FreshnessScore}");
        Console.WriteLine($"Stale context: {status.HasStaleContext}");
        Console.WriteLine($"Stale files: {status.StaleContextFiles.Count}");
        Console.WriteLine($"Missing generated files: {status.MissingGeneratedFiles.Count}");
        Console.WriteLine($"Manually modified files: {status.ManuallyModifiedFiles.Count}");
        foreach (var action in status.RecommendedActions) Console.WriteLine($"Recommended: {action}");
        return 0;
    }
    default:
        PrintUsage();
        return 1;
}

static string? OptionValue(string[] values, string name)
{
    var index = Array.FindIndex(values, value => value.Equals(name, StringComparison.OrdinalIgnoreCase));
    return index >= 0 && index + 1 < values.Length ? values[index + 1] : null;
}

static void PrintUsage()
{
    Console.WriteLine("sourceworker scan <path> [--instructions <instructionFilePath>]");
    Console.WriteLine("sourceworker import-instructions <projectPath> <instructionFilePath>");
    Console.WriteLine("sourceworker status");
    Console.WriteLine("sourceworker handoff <path>");
    Console.WriteLine("sourceworker doctor <path> [--json]");
    Console.WriteLine("sourceworker changes <path> [--json]");
    Console.WriteLine("sourceworker context-status <path>");
    Console.WriteLine("sourceworker recipes <path>");
    Console.WriteLine("sourceworker run-recipe <path> <recipeId> [--force]");
}
