using System.Text;
using SourceWorker.Application;
using SourceWorker.Domain;

namespace SourceWorker.Recipes;

public sealed class BasicRecipeRunner(IProjectScanner scanner, IContextWriter contextWriter, IProjectInstructionRepository instructions, IGeneratedArtifactRepository? artifacts = null) : IRecipeRunner
{
    public async Task<ContextWriteResult> RunBasicHandoffAsync(Project project, CancellationToken cancellationToken)
        => await contextWriter.WriteBasicContextAsync(await scanner.ScanAsync(project, cancellationToken), cancellationToken);

    public async Task<ContextWriteResult> RunBackendDocsCompactAsync(ProjectScanResult scanResult, CancellationToken cancellationToken)
        => await WriteBackendDocsCompactAsync(scanResult, await instructions.GetLatestAsync(scanResult.Project.Id, cancellationToken), cancellationToken);

    public async Task<ContextWriteResult> RunAsync(ProjectRecipe recipe, ProjectScanResult scanResult, ProjectInstructionDocument? instructionDocument, bool force, CancellationToken cancellationToken)
        => recipe.Id.Equals("backend-docs-compact", StringComparison.OrdinalIgnoreCase)
            ? await WriteBackendDocsCompactAsync(scanResult, instructionDocument, cancellationToken, force)
            : await WriteGenericRecipeAsync(recipe, scanResult, instructionDocument, cancellationToken);

    private async Task<ContextWriteResult> WriteBackendDocsCompactAsync(ProjectScanResult scan, ProjectInstructionDocument? doc, CancellationToken cancellationToken, bool force = false)
    {
        var recipeDirectory = Path.Combine(scan.Project.RootPath, ".ai-context", "recipes");
        var output = Path.Combine(scan.Project.RootPath, "instructions", "Result", "Backend");
        Directory.CreateDirectory(recipeDirectory);
        Directory.CreateDirectory(output);
        var recipe = new RecipeFactory().CreateDefaultRecipes(scan.Project).First(r => r.Id == "backend-docs-compact");
        await File.WriteAllTextAsync(Path.Combine(recipeDirectory, "backend-docs-compact.yaml"), FileRecipeRepository.ToYaml(recipe), cancellationToken);

        var files = new Dictionary<string, string>
        {
            ["README.md"] = Readme(scan, doc),
            ["01-project-structure.md"] = ProjectStructure(scan),
            ["02-architecture.md"] = Architecture(scan),
            ["03-important-files-and-flows.md"] = ImportantFilesAndFlows(scan, doc),
            ["04-configuration-and-secrets.md"] = Configuration(scan),
            ["05-tasks-and-risks.md"] = TasksAndRisks(scan, doc),
            ["06-verification-checklist.md"] = Verification(doc)
        };

        foreach (var file in files)
        {
            await WriteGeneratedAsync(scan, "backend-docs-compact", output, file.Key, file.Value, force, cancellationToken);
        }

        return new ContextWriteResult(output, files.Keys.Prepend(".ai-context/recipes/backend-docs-compact.yaml").ToArray());
    }

    private async Task WriteGeneratedAsync(ProjectScanResult scan, string recipeId, string outputRoot, string relativePath, string body, bool force, CancellationToken cancellationToken)
    {
        var absolute = Path.Combine(outputRoot, relativePath);
        var projectRelative = Path.GetRelativePath(scan.Project.RootPath, absolute).Replace('\\', '/');
        var previous = artifacts is null ? null : await artifacts.GetByPathAsync(scan.Project.Id, projectRelative, cancellationToken);
        if (previous is not null && File.Exists(absolute))
        {
            var currentHash = Hash(StripMetadata(await File.ReadAllTextAsync(absolute, cancellationToken)));
            if (!force && !currentHash.Equals(previous.ContentHash, StringComparison.OrdinalIgnoreCase))
            {
                await artifacts!.UpsertAsync(previous with { Status = GeneratedArtifactStatus.ManuallyModified, Notes = "Current file hash differs from last generated hash." }, cancellationToken);
                return;
            }
        }

        var inputHash = Hash(body);
        var content = Metadata(scan.Project.Id, scan.ScanRun.Id, recipeId, inputHash, inputHash, "Generated") + body;
        Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
        await File.WriteAllTextAsync(absolute, content, cancellationToken);
        if (artifacts is not null)
        {
            await artifacts.UpsertAsync(new GeneratedArtifact(Guid.NewGuid(), scan.Project.Id, scan.ScanRun.Id, recipeId, projectRelative, absolute, GeneratedArtifactType.BackendDocs, inputHash, DateTimeOffset.UtcNow, inputHash, GeneratedArtifactStatus.Generated, "backend-docs-compact", null), cancellationToken);
        }
    }

    private static string Hash(string content)
        => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant();

    private static string StripMetadata(string content)
    {
        if (!content.StartsWith("<!--", StringComparison.Ordinal)) return content;
        var end = content.IndexOf("-->", StringComparison.Ordinal);
        return end < 0 ? content : content[(end + 3)..].TrimStart();
    }

    private static string Metadata(Guid projectId, Guid? scanRunId, string? recipeId, string inputHash, string contentHash, string status)
        => $"""
        <!--
        GeneratedBy: SourceWorker
        FormatVersion: 2
        ProjectId: {projectId}
        ScanRunId: {scanRunId}
        RecipeId: {recipeId}
        GeneratedUtc: {DateTimeOffset.UtcNow:O}
        InputHash: {inputHash}
        ContentHash: {contentHash}
        Status: {status}
        -->

        """;

    private static async Task<ContextWriteResult> WriteGenericRecipeAsync(ProjectRecipe recipe, ProjectScanResult scan, ProjectInstructionDocument? doc, CancellationToken cancellationToken)
    {
        var output = Path.Combine(scan.Project.RootPath, recipe.OutputPath);
        Directory.CreateDirectory(output);
        var fileName = $"{recipe.Id}.md";
        var content = $"""
        # {recipe.Name}

        {recipe.Description}

        ## Included Sections

        {ListOrNone(recipe.IncludedSections.Select(s => $"- {s}"))}

        ## Source Scopes

        {ListOrNone(recipe.SourceScopes.Select(s => $"- `{s}`"))}

        ## Imported Instructions

        {(doc is null ? "- None imported" : $"- {doc.Title} from `{doc.SourceFilePath}`")}
        """;
        await File.WriteAllTextAsync(Path.Combine(output, fileName), content, cancellationToken);
        return new ContextWriteResult(output, [fileName]);
    }

    private static string Readme(ProjectScanResult scan, ProjectInstructionDocument? doc)
        => $"""
        # Backend Documentation

        Generated by SourceWorker recipe `backend-docs-compact`.

        - Project: {scan.Project.Name}
        - Root: `{scan.Project.RootPath}`
        - Solution: {scan.SolutionPath ?? "none detected"}
        - Technologies: {TechnologySummary(scan)}
        - Projects: {scan.Projects.Count}
        - Test projects: {scan.Projects.Count(p => p.IsTestProject)}
        - Imported instructions: {(doc is null ? "none" : doc.Title)}

        Use these compact notes as developer handoff material. They are scanner and instruction derived, not AI-authored.
        """;

    private static string ProjectStructure(ProjectScanResult scan)
        => $"""
        # Project Structure

        ## Folders

        {ListOrNone(scan.FolderRoles.Select(f => $"- `{f.RelativePath}`: {f.Description}"))}

        ## Projects

        {ListOrNone(scan.Projects.Select(p => $"- `{p.RelativePath}` ({p.Sdk})"))}

        ## Tests

        {ListOrNone(scan.Projects.Where(p => p.IsTestProject).Select(p => $"- `{p.RelativePath}`"))}
        """;

    private static string Architecture(ProjectScanResult scan)
        => $"""
        # Architecture

        ## Detected Shape

        {ArchitectureStyle(scan)}

        ## Project References

        {ListOrNone(scan.Projects.SelectMany(p => p.ProjectReferences.Select(r => $"- `{p.RelativePath}` -> `{r}`")))}
        """;

    private static string ImportantFilesAndFlows(ProjectScanResult scan, ProjectInstructionDocument? doc)
        => $"""
        # Important Files And Flows

        ## Entrypoints

        {ListOrNone(scan.MainEntrypoints.Select(e => $"- `{e}`"))}

        ## Imported Source Paths

        {ListOrNone((doc?.SourcePaths ?? []).Select(p => $"- `{p.OriginalPath}` -> `{p.NormalizedPath}` ({(p.Exists ? "found" : "missing")})"))}

        ## Rules And Mindset

        {ListOrNone((doc?.Rules ?? []).Select(r => $"- {r.Text}"))}
        """;

    private static string Configuration(ProjectScanResult scan)
        => $"""
        # Configuration And Secrets

        ## Config Files

        {ListOrNone(scan.ConfigFiles.Select(c => $"- `{c}`"))}

        ## Docker Files

        {ListOrNone(scan.DockerFiles.Select(d => $"- `{d}`"))}

        ## Secret Handling

        - Do not store secrets in `.ai-context`, recipe files, or Localite.
        - Treat configuration docs as references only; redact sensitive values.
        """;

    private static string TasksAndRisks(ProjectScanResult scan, ProjectInstructionDocument? doc)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Tasks And Risks");
        builder.AppendLine();
        builder.AppendLine("## Scanner TODO/FIXME/REVIEW");
        builder.AppendLine();
        builder.AppendLine(ListOrNone(scan.Todos.Select(t => $"- {t.Kind} `{t.RelativePath}:{t.LineNumber}` {t.Text}")));
        builder.AppendLine();
        builder.AppendLine("## Imported Todos");
        builder.AppendLine();
        builder.AppendLine(ListOrNone((doc?.Todos ?? []).Select(t => $"- {(t.IsCompleted ? "[x]" : "[ ]")} {t.Text}")));
        builder.AppendLine();
        builder.AppendLine("## Skip Later");
        builder.AppendLine();
        builder.AppendLine(ListOrNone((doc?.SkipItems ?? []).Select(s => $"- {s.Text}")));
        return builder.ToString();
    }

    private static string Verification(ProjectInstructionDocument? doc)
        => $"""
        # Verification Checklist

        {ListOrNone((doc?.VerificationItems ?? []).Select(v => $"- {(v.IsCompleted ? "[x]" : "[ ]")} {v.Text}"))}

        ## Default Checks

        - Build the solution.
        - Run relevant tests.
        - Re-run SourceWorker scan after meaningful changes.
        """;

    private static string TechnologySummary(ProjectScanResult scan)
        => scan.Technologies.Count == 0 ? "none detected" : string.Join(", ", scan.Technologies.Select(t => t.Kind).Distinct());

    private static string ArchitectureStyle(ProjectScanResult scan)
        => scan.FolderRoles.Any(f => f.Role == "core") && scan.FolderRoles.Any(f => f.Role == "infrastructure")
            ? "Clean architecture or layered backend structure."
            : "No strong architecture style detected yet.";

    private static string ListOrNone(IEnumerable<string> lines)
    {
        var materialized = lines.Where(line => !string.IsNullOrWhiteSpace(line)).ToArray();
        return materialized.Length == 0 ? "- None detected" : string.Join(Environment.NewLine, materialized);
    }
}
