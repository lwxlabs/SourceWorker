using System.Diagnostics;

namespace SourceWorker.Tests.Application;

public sealed class CliCommandTests
{
    [Fact]
    public async Task Cli_instruction_recipe_and_doctor_commands_work()
    {
        var repoRoot = FindRepoRoot();
        var projectRoot = Path.Combine(Path.GetTempPath(), "sourceworker-cli-" + Guid.NewGuid());
        Directory.CreateDirectory(Path.Combine(projectRoot, "src"));
        await File.WriteAllTextAsync(Path.Combine(projectRoot, "CliSample.sln"), "");
        await File.WriteAllTextAsync(Path.Combine(projectRoot, "src", "CliSample.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk.Web\"></Project>");
        await File.WriteAllTextAsync(Path.Combine(projectRoot, "src", "Program.cs"), "public class Program { public void Run() { } } // FIXME: sample");
        var instructions = Path.Combine(projectRoot, "todo-louie.md");
        await File.WriteAllTextAsync(instructions, """
            ## files
            - `src/Program.cs`
            ## mindset
            - do not guess
            ## todo
            - wire cli
            ## finally
            - run tests
            """);

        await Dotnet(repoRoot, $"run --no-build --project src/services/SourceWorker.Cli -- scan \"{projectRoot}\" --instructions \"{instructions}\"");
        var doctor = await Dotnet(repoRoot, $"run --no-build --project src/services/SourceWorker.Cli -- doctor \"{projectRoot}\" --json");
        var changes = await Dotnet(repoRoot, $"run --no-build --project src/services/SourceWorker.Cli -- changes \"{projectRoot}\" --json");
        var contextStatus = await Dotnet(repoRoot, $"run --no-build --project src/services/SourceWorker.Cli -- context-status \"{projectRoot}\"");
        var storageInfo = await Dotnet(repoRoot, "run --no-build --project src/services/SourceWorker.Cli -- storage-info --json");
        var artifacts = await Dotnet(repoRoot, $"run --no-build --project src/services/SourceWorker.Cli -- artifacts \"{projectRoot}\" --json");
        var cleanupPreview = await Dotnet(repoRoot, $"run --no-build --project src/services/SourceWorker.Cli -- cleanup-preview \"{projectRoot}\" --json");
        var cleanupDryRun = await Dotnet(repoRoot, $"run --no-build --project src/services/SourceWorker.Cli -- cleanup-generated \"{projectRoot}\" --dry-run");
        var symbols = await Dotnet(repoRoot, $"run --no-build --project src/services/SourceWorker.Cli -- symbols \"{projectRoot}\" --json");
        var flows = await Dotnet(repoRoot, $"run --no-build --project src/services/SourceWorker.Cli -- flows \"{projectRoot}\" --json");
        var routes = await Dotnet(repoRoot, $"run --no-build --project src/services/SourceWorker.Cli -- routes \"{projectRoot}\" --json");
        var contextFiles = await Dotnet(repoRoot, $"run --no-build --project src/services/SourceWorker.Cli -- context-files \"{projectRoot}\" --json");
        var handoff = await Dotnet(repoRoot, $"run --no-build --project src/services/SourceWorker.Cli -- read-context \"{projectRoot}\" .ai-context/ai-handoff.md");
        var internalReadme = await Dotnet(repoRoot, $"run --no-build --project src/services/SourceWorker.Cli -- read-context \"{projectRoot}\" .ai-context/docs/backend/README.md");
        var outputs = await Dotnet(repoRoot, $"run --no-build --project src/services/SourceWorker.Cli -- outputs \"{projectRoot}\" --json");
        var packs = await Dotnet(repoRoot, $"run --no-build --project src/services/SourceWorker.Cli -- packs \"{projectRoot}\" --json");
        var packShow = await Dotnet(repoRoot, $"run --no-build --project src/services/SourceWorker.Cli -- pack-show \"{projectRoot}\" backend-docs");
        var packExportPath = Path.Combine(projectRoot, ".ai-context", "packs", "backend-docs.md");
        var packExport = await Dotnet(repoRoot, $"run --no-build --project src/services/SourceWorker.Cli -- pack-export \"{projectRoot}\" backend-docs --out \"{packExportPath}\"");
        var exportDocs = await Dotnet(repoRoot, $"run --no-build --project src/services/SourceWorker.Cli -- export-docs \"{projectRoot}\" backend-docs-compact");
        var recipes = await Dotnet(repoRoot, $"run --no-build --project src/services/SourceWorker.Cli -- recipes \"{projectRoot}\"");
        var runRecipe = await Dotnet(repoRoot, $"run --no-build --project src/services/SourceWorker.Cli -- run-recipe \"{projectRoot}\" backend-docs-compact");

        Assert.Contains("ImportedInstructionsFound", doctor);
        Assert.Contains("NewFiles", changes);
        Assert.Contains("Freshness score:", contextStatus);
        Assert.Contains("table-backed", storageInfo);
        Assert.Contains("Artifacts", artifacts);
        Assert.Contains("SafeDeleteFiles", cleanupPreview);
        Assert.Contains("Dry run: True", cleanupDryRun);
        Assert.Contains("Program", symbols);
        Assert.Contains("Scan Project", flows);
        Assert.Contains("[", routes);
        Assert.Contains("ai-handoff.md", contextFiles);
        Assert.Contains("AI Handoff", handoff);
        Assert.Contains("Backend Documentation", internalReadme);
        Assert.Contains("internal-backend-docs", outputs);
        Assert.Contains("backend-docs", packs);
        Assert.Contains("Valid: True", packShow);
        Assert.Contains("Exported pack:", packExport);
        Assert.Contains("Exported docs:", exportDocs);
        Assert.Contains("backend-docs-compact", recipes);
        Assert.Contains("Ran recipe: backend-docs-compact", runRecipe);
        Assert.True(File.Exists(Path.Combine(projectRoot, ".ai-context", "recipes", "imported-todo-louie.yaml")));
        Assert.True(File.Exists(Path.Combine(projectRoot, ".ai-context", "docs", "backend", "06-verification-checklist.md")));
        Assert.True(File.Exists(packExportPath));
        Assert.True(File.Exists(Path.Combine(projectRoot, "instructions", "Result", "Backend", "06-verification-checklist.md")));
    }

    private static async Task<string> Dotnet(string workingDirectory, string arguments)
    {
        using var process = Process.Start(new ProcessStartInfo("dotnet", arguments)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        })!;

        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        Assert.True(process.ExitCode == 0, output + Environment.NewLine + error);
        return output + error;
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "SourceWorker.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Could not find SourceWorker.slnx.");
    }
}
