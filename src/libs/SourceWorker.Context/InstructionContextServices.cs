using System.Text;
using SourceWorker.Application;
using SourceWorker.Domain;

namespace SourceWorker.Context;

public sealed class SourcePathValidator : ISourcePathValidator
{
    public IReadOnlyList<ProjectInstructionSourcePath> Validate(string projectRootPath, IEnumerable<string> paths)
    {
        var root = Path.GetFullPath(projectRootPath);
        return paths.Select(path => ValidateOne(root, path)).ToArray();
    }

    private static ProjectInstructionSourcePath ValidateOne(string root, string original)
    {
        var normalized = original.Trim().Trim('`', '"', '\'').Replace('\\', Path.DirectorySeparatorChar);
        var candidate = Path.IsPathRooted(normalized) ? Path.GetFullPath(normalized) : Path.GetFullPath(Path.Combine(root, normalized));
        var inside = candidate.Equals(root, StringComparison.OrdinalIgnoreCase) || candidate.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        var existsFile = File.Exists(candidate);
        var existsDirectory = Directory.Exists(candidate);
        var type = existsFile ? SourcePathType.File : existsDirectory ? SourcePathType.Folder : SourcePathType.Unknown;
        var warning = !inside ? "Path is outside the project root." : !existsFile && !existsDirectory ? "Path was not found." : null;
        return new ProjectInstructionSourcePath(original, Path.GetRelativePath(root, candidate).Replace('\\', '/'), existsFile || existsDirectory, type, inside, warning);
    }
}

public sealed class InstructionContextWriter : IInstructionContextWriter
{
    public async Task WriteImportedInstructionsAsync(Project project, ProjectInstructionDocument instructions, CancellationToken cancellationToken)
    {
        var output = Path.Combine(project.RootPath, ".ai-context", "instructions", "imported");
        Directory.CreateDirectory(output);
        var rendered = RenderInstructions(instructions);
        await File.WriteAllTextAsync(Path.Combine(output, "imported-instructions.md"), rendered, cancellationToken);
        var legacy = Path.Combine(project.RootPath, ".ai-context", "instructions", "imported-instructions.md");
        if (File.Exists(legacy)) await File.WriteAllTextAsync(legacy, rendered, cancellationToken);
    }

    public async Task WriteSourcePathValidationAsync(Project project, SourcePathValidationReport report, CancellationToken cancellationToken)
    {
        var output = Path.Combine(project.RootPath, ".ai-context", "instructions", "imported");
        Directory.CreateDirectory(output);
        var rendered = RenderValidation(report);
        await File.WriteAllTextAsync(Path.Combine(output, "source-path-validation.md"), rendered, cancellationToken);
        var legacy = Path.Combine(project.RootPath, ".ai-context", "instructions", "source-path-validation.md");
        if (File.Exists(legacy)) await File.WriteAllTextAsync(legacy, rendered, cancellationToken);
    }

    private static string RenderInstructions(ProjectInstructionDocument doc)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"# Imported Instructions: {doc.Title}");
        builder.AppendLine();
        builder.AppendLine($"- Source file: `{doc.SourceFilePath}`");
        builder.AppendLine($"- Imported at: {doc.ImportedAt:O}");
        builder.AppendLine();
        builder.AppendLine("## Source Paths");
        builder.AppendLine(ListOrNone(doc.SourcePaths.Select(p => $"- `{p.OriginalPath}`")));
        builder.AppendLine();
        builder.AppendLine("## Rules And Mindset");
        builder.AppendLine(ListOrNone(doc.Rules.Select(r => $"- {r.Text}")));
        builder.AppendLine();
        builder.AppendLine("## Todos");
        builder.AppendLine(ListOrNone(doc.Todos.Select(t => $"- {(t.IsCompleted ? "[x]" : "[ ]")} {t.Text}")));
        builder.AppendLine();
        builder.AppendLine("## Skip Later");
        builder.AppendLine(ListOrNone(doc.SkipItems.Select(s => $"- {s.Text}")));
        builder.AppendLine();
        builder.AppendLine("## Verification");
        builder.AppendLine(ListOrNone(doc.VerificationItems.Select(v => $"- {(v.IsCompleted ? "[x]" : "[ ]")} {v.Text}")));
        builder.AppendLine();
        builder.AppendLine("## All Sections");
        foreach (var section in doc.Sections)
        {
            builder.AppendLine($"### {section.Title}");
            builder.AppendLine();
            builder.AppendLine(section.RawContent);
            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static string RenderValidation(SourcePathValidationReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Source Path Validation");
        builder.AppendLine();
        foreach (var path in report.Paths)
        {
            builder.AppendLine($"- `{path.OriginalPath}`");
            builder.AppendLine($"  - normalized: `{path.NormalizedPath}`");
            builder.AppendLine($"  - exists: {path.Exists}");
            builder.AppendLine($"  - type: {path.Type}");
            builder.AppendLine($"  - insideProject: {path.IsInsideProject}");
            if (path.WarningMessage is not null) builder.AppendLine($"  - warning: {path.WarningMessage}");
        }

        if (report.Paths.Count == 0) builder.AppendLine("- No source paths imported.");
        return builder.ToString();
    }

    private static string ListOrNone(IEnumerable<string> lines)
    {
        var materialized = lines.ToArray();
        return materialized.Length == 0 ? "- None" : string.Join(Environment.NewLine, materialized);
    }
}

public sealed class DoctorService(IProjectScanner scanner, IProjectInstructionRepository instructions, IRecipeRepository recipes, ISourcePathValidator validator, IContextFreshnessService freshnessService, IScanRunRepository scanRuns, IGeneratedArtifactRepository artifacts) : IDoctorService
{
    public async Task<DoctorReport> CheckAsync(Project project, CancellationToken cancellationToken)
    {
        var rootFound = Directory.Exists(project.RootPath);
        var contextFolder = Path.Combine(project.RootPath, ".ai-context");
        var scan = rootFound ? await scanner.ScanAsync(project, cancellationToken) : null;
        var imported = await instructions.GetLatestAsync(project.Id, cancellationToken);
        var recipeList = rootFound ? await recipes.ListAsync(project, cancellationToken) : [];
        var artifactList = rootFound ? await artifacts.GetByProjectAsync(project.Id, cancellationToken) : [];
        var latestScan = await scanRuns.GetLatestAsync(project.Id, cancellationToken);
        var missing = imported is null ? [] : validator.Validate(project.RootPath, imported.SourcePaths.Select(p => p.OriginalPath)).Where(p => !p.Exists || !p.IsInsideProject).ToArray();
        var freshness = await freshnessService.CheckAsync(project, cancellationToken);
        var lastScan = latestScan?.CompletedAt ?? scan?.ScanRun.CompletedAt;
        var stale = freshness.HasStaleContext;
        var warnings = missing.Select(p => $"{p.OriginalPath}: {p.WarningMessage}").ToArray();

        return new DoctorReport(
            project.RootPath,
            rootFound,
            Directory.Exists(Path.Combine(project.RootPath, ".git")),
            scan?.SolutionFiles.Select(s => s.RelativePath).ToArray() ?? [],
            scan?.Projects.Select(p => p.RelativePath).ToArray() ?? [],
            scan?.Projects.Where(p => p.IsTestProject).Select(p => p.RelativePath).ToArray() ?? [],
            Directory.Exists(contextFolder),
            imported is not null,
            recipeList.Select(r => r.Id).ToArray(),
            missing,
            stale,
            lastScan,
            warnings.Concat(freshness.RecommendedActions.Select(action => $"Recommended: {action}")).Concat(artifactList.Where(a => !File.Exists(a.AbsolutePath)).Select(a => $"Missing generated file: {a.RelativePath}")).ToArray());
    }
}
