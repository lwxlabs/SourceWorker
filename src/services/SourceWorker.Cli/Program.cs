using System.Text.Json;
using LwX.Flow;
using Microsoft.Extensions.DependencyInjection;
using SourceWorker.Application;
using SourceWorker.Infrastructure;
using SourceWorker.Worker;

var services = new ServiceCollection()
    .AddSourceWorkerApplication()
    .AddSourceWorkerInfrastructure(options => options.DatabasePath = Environment.GetEnvironmentVariable("SOURCEWORKER_STORAGE_PATH"))
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
    case "storage-info":
    {
        var info = await flow.Send(new GetStorageInfoQuery());
        if (args.Contains("--json", StringComparer.OrdinalIgnoreCase))
        {
            Console.WriteLine(JsonSerializer.Serialize(info, new JsonSerializerOptions { WriteIndented = true }));
        }
        else
        {
            Console.WriteLine($"Storage provider: {info.Schema.Provider}");
            Console.WriteLine($"Storage mode: {info.Schema.Mode}");
            Console.WriteLine($"Storage path: {info.Schema.StoragePath}");
            Console.WriteLine($"Schema version: {info.Schema.SchemaVersion}");
            Console.WriteLine($"Projects: {info.ProjectCount}");
            Console.WriteLine($"Scan runs: {info.ScanRunCount}");
            Console.WriteLine($"Indexed files: {info.IndexedFileCount}");
            Console.WriteLine($"Generated artifacts: {info.GeneratedArtifactCount}");
            Console.WriteLine($"Jobs: {info.JobCount}");
            Console.WriteLine($"Instructions: {info.InstructionCount}");
            Console.WriteLine($"Recipes: {info.RecipeCount}");
            Console.WriteLine($"Code symbols: {info.CodeSymbolCount}");
            Console.WriteLine($"Code flows: {info.CodeFlowCount}");
            Console.WriteLine($"Routes: {info.RouteCount}");
            Console.WriteLine($"Service registrations: {info.ServiceRegistrationCount}");
            Console.WriteLine($"Configuration usages: {info.ConfigurationUsageCount}");
            Console.WriteLine($"Tests: {info.TestInfoCount}");
        }

        return 0;
    }
    case "artifacts" when args.Length >= 2:
    {
        var project = await flow.Send(new AddProjectCommand(args[1]));
        var report = await flow.Send(new GetArtifactsQuery(project.Id));
        if (args.Contains("--json", StringComparer.OrdinalIgnoreCase))
        {
            Console.WriteLine(JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        }
        else
        {
            foreach (var artifact in report.Artifacts)
            {
                Console.WriteLine($"{artifact.Status} {artifact.RecipeId ?? "-"} {artifact.RelativePath} {artifact.GeneratedUtc:O}");
            }
            foreach (var path in report.OrphanedArtifacts) Console.WriteLine($"Orphaned {path}");
            foreach (var path in report.DeprecatedArtifacts) Console.WriteLine($"Deprecated {path}");
        }

        return 0;
    }
    case "cleanup-preview" when args.Length >= 2:
    {
        var project = await flow.Send(new AddProjectCommand(args[1]));
        var preview = await flow.Send(new GetCleanupPreviewQuery(project.Id));
        if (args.Contains("--json", StringComparer.OrdinalIgnoreCase))
        {
            Console.WriteLine(JsonSerializer.Serialize(preview, new JsonSerializerOptions { WriteIndented = true }));
        }
        else
        {
            Console.WriteLine("Safe cleanup candidates:");
            foreach (var file in preview.SafeDeleteFiles) Console.WriteLine(file);
            foreach (var warning in preview.Warnings) Console.WriteLine($"Warning: {warning}");
        }

        return 0;
    }
    case "cleanup-generated" when args.Length >= 2:
    {
        var project = await flow.Send(new AddProjectCommand(args[1]));
        var result = await flow.Send(new CleanupGeneratedCommand(project.Id, args.Contains("--dry-run", StringComparer.OrdinalIgnoreCase), args.Contains("--force", StringComparer.OrdinalIgnoreCase)));
        Console.WriteLine($"Dry run: {result.DryRun}");
        foreach (var file in result.DeletedFiles) Console.WriteLine(result.DryRun ? $"Would delete: {file}" : $"Deleted: {file}");
        foreach (var file in result.SkippedFiles) Console.WriteLine($"Skipped: {file}");
        foreach (var warning in result.Warnings) Console.WriteLine($"Warning: {warning}");
        return 0;
    }
    case "symbols" when args.Length >= 2:
    {
        var project = await flow.Send(new AddProjectCommand(args[1]));
        var result = await flow.Send(new GetSymbolsQuery(project.Id));
        if (args.Contains("--json", StringComparer.OrdinalIgnoreCase)) Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
        else foreach (var group in result.GroupBy(s => s.RelativePath).OrderBy(g => g.Key)) { Console.WriteLine(group.Key); foreach (var s in group.Take(50)) Console.WriteLine($"  {s.Kind} {s.FullName}:{s.LineNumber}"); }
        return 0;
    }
    case "flows" when args.Length >= 2:
    {
        var project = await flow.Send(new AddProjectCommand(args[1]));
        var result = await flow.Send(new GetFlowsQuery(project.Id));
        if (args.Contains("--json", StringComparer.OrdinalIgnoreCase)) Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
        else foreach (var f in result) Console.WriteLine($"{f.Confidence} {f.FlowType} {f.Name} - {f.Trigger}");
        return 0;
    }
    case "routes" when args.Length >= 2:
    {
        var project = await flow.Send(new AddProjectCommand(args[1]));
        var result = await flow.Send(new GetRoutesQuery(project.Id));
        if (args.Contains("--json", StringComparer.OrdinalIgnoreCase)) Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
        else foreach (var r in result) Console.WriteLine($"{r.HttpMethod} {r.RoutePattern} {r.RelativePath}:{r.LineNumber}");
        return 0;
    }
    case "context-files" when args.Length >= 2:
    {
        var project = await flow.Send(new AddProjectCommand(args[1]));
        var result = await flow.Send(new GetContextFilesQuery(project.Id));
        if (args.Contains("--json", StringComparer.OrdinalIgnoreCase)) Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
        else foreach (var f in result) Console.WriteLine($"{f.RelativePath} ({f.SizeBytes} bytes)");
        return 0;
    }
    case "read-context" when args.Length >= 3:
    {
        var project = await flow.Send(new AddProjectCommand(args[1]));
        Console.WriteLine(await flow.Send(new ReadContextFileQuery(project.Id, args[2])));
        return 0;
    }
    case "outputs" when args.Length >= 2:
    {
        var project = await flow.Send(new AddProjectCommand(args[1]));
        var result = await flow.Send(new GetOutputsQuery(project.Id));
        if (args.Contains("--json", StringComparer.OrdinalIgnoreCase)) Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
        else
        {
            Console.WriteLine($"Recipe: {result.RecipeId}");
            foreach (var destination in result.Destinations) Console.WriteLine($"{destination.Id} {destination.Type} {destination.Path} primary={destination.Primary}");
            foreach (var file in result.Files) Console.WriteLine($"{file.Status} {file.ArtifactType} {file.RelativePath}");
        }
        return 0;
    }
    case "packs" when args.Length >= 2:
    {
        var project = await flow.Send(new AddProjectCommand(args[1]));
        var result = await flow.Send(new GetPacksQuery(project.Id));
        if (args.Contains("--json", StringComparer.OrdinalIgnoreCase)) Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
        else foreach (var pack in result) Console.WriteLine($"{pack.Pack.Id} - {pack.Pack.Name} ({pack.Pack.Items.Count} files, valid={pack.IsValid})");
        return 0;
    }
    case "pack-show" when args.Length >= 3:
    {
        var project = await flow.Send(new AddProjectCommand(args[1]));
        var result = await flow.Send(new GetPackQuery(project.Id, args[2])) ?? throw new InvalidOperationException($"Pack '{args[2]}' was not found.");
        if (args.Contains("--json", StringComparer.OrdinalIgnoreCase)) Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
        else
        {
            Console.WriteLine($"{result.Pack.Id} - {result.Pack.Name}");
            Console.WriteLine($"Valid: {result.IsValid}");
            foreach (var item in result.Pack.Items) Console.WriteLine($"- {item.RelativePath}");
            foreach (var missing in result.MissingFiles) Console.WriteLine($"Missing: {missing}");
        }
        return 0;
    }
    case "pack-export" when args.Length >= 5:
    {
        var project = await flow.Send(new AddProjectCommand(args[1]));
        var output = OptionValue(args, "--out") ?? throw new InvalidOperationException("--out is required.");
        var written = await flow.Send(new ExportPackCommand(project.Id, args[2], output));
        Console.WriteLine($"Exported pack: {written}");
        return 0;
    }
    case "export-docs" when args.Length >= 3:
    {
        var project = await flow.Send(new AddProjectCommand(args[1]));
        var run = await flow.Send(new ExportDocsCommand(project.Id, args[2]));
        Console.WriteLine($"Exported docs: {run.ExportProfileId}");
        foreach (var file in run.Files) Console.WriteLine(file);
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
    Console.WriteLine("sourceworker storage-info [--json]");
    Console.WriteLine("sourceworker symbols <path> [--json]");
    Console.WriteLine("sourceworker flows <path> [--json]");
    Console.WriteLine("sourceworker routes <path> [--json]");
    Console.WriteLine("sourceworker context-files <path> [--json]");
    Console.WriteLine("sourceworker read-context <path> <relativePath>");
    Console.WriteLine("sourceworker outputs <path> [--json]");
    Console.WriteLine("sourceworker packs <path> [--json]");
    Console.WriteLine("sourceworker pack-show <path> <packId> [--json]");
    Console.WriteLine("sourceworker pack-export <path> <packId> --out <outputFile>");
    Console.WriteLine("sourceworker export-docs <path> <exportProfileId>");
    Console.WriteLine("sourceworker artifacts <path> [--json]");
    Console.WriteLine("sourceworker cleanup-preview <path> [--json]");
    Console.WriteLine("sourceworker cleanup-generated <path> [--dry-run] [--force]");
    Console.WriteLine("sourceworker recipes <path>");
    Console.WriteLine("sourceworker run-recipe <path> <recipeId> [--force]");
}
