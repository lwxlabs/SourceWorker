using System.Text;
using SourceWorker.Application;
using SourceWorker.Domain;

namespace SourceWorker.Recipes;

public sealed class BasicRecipeRunner(IProjectScanner scanner, IContextWriter contextWriter, IProjectInstructionRepository instructions, IGeneratedArtifactRepository? artifacts = null, ICodeFlowRepository? flows = null, IRouteInfoRepository? routes = null, IServiceRegistrationRepository? services = null, IConfigurationUsageRepository? configs = null, IBackgroundWorkerInfoRepository? workers = null, ITestInfoRepository? tests = null, IFlowDiagramGenerator? diagrams = null, IExportProfileService? exportProfiles = null, IContextPackWriter? packWriter = null) : IRecipeRunner
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
        var output = Path.Combine(scan.Project.RootPath, ".ai-context", "docs", "backend");
        var externalOutput = Path.Combine(scan.Project.RootPath, "instructions", "Result", "Backend");
        Directory.CreateDirectory(recipeDirectory);
        Directory.CreateDirectory(output);
        Directory.CreateDirectory(externalOutput);
        var recipe = new RecipeFactory().CreateDefaultRecipes(scan.Project).First(r => r.Id == "backend-docs-compact");
        await File.WriteAllTextAsync(Path.Combine(recipeDirectory, "backend-docs-compact.yaml"), FileRecipeRepository.ToYaml(recipe), cancellationToken);

        var codeFlows = flows is null ? [] : await flows.GetByProjectAsync(scan.Project.Id, cancellationToken);
        var routeInfos = routes is null ? [] : await routes.GetByProjectAsync(scan.Project.Id, cancellationToken);
        var serviceRegs = services is null ? [] : await services.GetByProjectAsync(scan.Project.Id, cancellationToken);
        var configUsages = configs is null ? [] : await configs.GetByProjectAsync(scan.Project.Id, cancellationToken);
        var workerInfos = workers is null ? [] : await workers.GetByProjectAsync(scan.Project.Id, cancellationToken);
        var testInfos = tests is null ? [] : await tests.GetByProjectAsync(scan.Project.Id, cancellationToken);

        var files = new Dictionary<string, string>
        {
            ["README.md"] = Readme(scan, doc),
            ["01-project-structure.md"] = ProjectStructure(scan),
            ["02-architecture.md"] = Architecture(scan),
            ["03-important-files-and-flows.md"] = ImportantFilesAndFlows(scan, doc, codeFlows, routeInfos, serviceRegs, workerInfos),
            ["04-configuration-and-secrets.md"] = Configuration(scan, configUsages),
            ["05-tasks-and-risks.md"] = TasksAndRisks(scan, doc),
            ["06-verification-checklist.md"] = Verification(doc, testInfos)
        };

        var generated = new List<GeneratedArtifact>();
        foreach (var file in files)
        {
            var internalArtifact = await WriteGeneratedAsync(scan, "backend-docs-compact", output, file.Key, file.Value, force, GeneratedArtifactType.InternalGeneratedDoc, "internal-backend-docs", cancellationToken);
            if (internalArtifact is not null) generated.Add(internalArtifact);
            var externalArtifact = await WriteGeneratedAsync(scan, "backend-docs-compact", externalOutput, file.Key, file.Value, force, GeneratedArtifactType.ExternalExportedDoc, "external-backend-docs", cancellationToken);
            if (externalArtifact is not null) generated.Add(externalArtifact);
        }

        await WriteDiagramsAsync(scan, codeFlows, cancellationToken);
        if (exportProfiles is not null) await exportProfiles.WriteBackendDocsProfileAsync(scan.Project, scan.ScanRun.Id, generated, cancellationToken);
        if (packWriter is not null) await packWriter.WriteDefaultPacksAsync(scan.Project, scan.ScanRun.Id, cancellationToken);

        return new ContextWriteResult(output, files.Keys.Select(f => $".ai-context/docs/backend/{f}").Prepend(".ai-context/recipes/backend-docs-compact.yaml").ToArray());
    }

    private async Task<GeneratedArtifact?> WriteGeneratedAsync(ProjectScanResult scan, string recipeId, string outputRoot, string relativePath, string body, bool force, GeneratedArtifactType type, string destinationId, CancellationToken cancellationToken)
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
                return null;
            }
        }

        var inputHash = Hash(body);
        var artifactId = Guid.NewGuid();
        var content = Metadata(artifactId, "Generated") + body;
        Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
        await File.WriteAllTextAsync(absolute, content, cancellationToken);
        GeneratedArtifact? artifact = null;
        if (artifacts is not null)
        {
            artifact = await artifacts.UpsertAsync(new GeneratedArtifact(artifactId, scan.Project.Id, scan.ScanRun.Id, recipeId, projectRelative, absolute, type, inputHash, DateTimeOffset.UtcNow, inputHash, GeneratedArtifactStatus.Generated, "backend-docs-compact", $"OutputDestinationId: {destinationId}"), cancellationToken);
        }
        return artifact;
    }

    private async Task WriteDiagramsAsync(ProjectScanResult scan, IReadOnlyList<CodeFlow> codeFlows, CancellationToken cancellationToken)
    {
        if (diagrams is null || artifacts is null) return;
        var output = Path.Combine(scan.Project.RootPath, ".ai-context", "diagrams");
        Directory.CreateDirectory(output);
        foreach (var flow in codeFlows.Where(f => f.Confidence == FlowConfidence.High).Take(10))
        {
            var fileName = $"{Slug(flow.Name)}.mmd";
            var body = diagrams.Generate(flow);
            var absolute = Path.Combine(output, fileName);
            await File.WriteAllTextAsync(absolute, body, cancellationToken);
            var relative = Path.GetRelativePath(scan.Project.RootPath, absolute).Replace('\\', '/');
            var hash = Hash(body);
            await artifacts.UpsertAsync(new GeneratedArtifact(Guid.NewGuid(), scan.Project.Id, scan.ScanRun.Id, "backend-docs-compact", relative, absolute, GeneratedArtifactType.DiagramFile, hash, DateTimeOffset.UtcNow, hash, GeneratedArtifactStatus.Generated, "SourceWorker.FlowDiagram", null), cancellationToken);
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

    private static string Metadata(Guid artifactId, string status)
        => $"<!-- SourceWorker:Generated; ArtifactId={artifactId}; Status={status} -->{Environment.NewLine}{Environment.NewLine}";

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

        - Canonical internal location: `.ai-context/docs/backend`
        - External mirrored location: `instructions/Result/Backend`
        - Export profile: `.ai-context/exports/backend-docs-compact.yaml`

        - Project: {scan.Project.Name}
        - Root: `{scan.Project.RootPath}`
        - Solution: {scan.SolutionPath ?? "none detected"}
        - Technologies: {TechnologySummary(scan)}
        - Projects: {scan.Projects.Count}
        - Test projects: {scan.Projects.Count(p => p.IsTestProject)}
        - Imported instructions: {(doc is null ? "none" : doc.Title)}

        ## Generated Files

        - `README.md`
        - `01-project-structure.md`
        - `02-architecture.md`
        - `03-important-files-and-flows.md`
        - `04-configuration-and-secrets.md`
        - `05-tasks-and-risks.md`
        - `06-verification-checklist.md`

        ## Verification And Export

        - `dotnet build SourceWorker.slnx`
        - `dotnet test SourceWorker.slnx --no-build`
        - `dotnet run --project src/services/SourceWorker.Cli -- export-docs <path> backend-docs-compact`
        - `dotnet run --project src/services/SourceWorker.Cli -- packs <path>`
        - `dotnet run --project src/services/SourceWorker.Cli -- pack-export <path> backend-docs --out <file>`

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

    private string ImportantFilesAndFlows(ProjectScanResult scan, ProjectInstructionDocument? doc, IReadOnlyList<CodeFlow> flows, IReadOnlyList<RouteInfo> routes, IReadOnlyList<ServiceRegistration> services, IReadOnlyList<BackgroundWorkerInfo> workers)
        => $"""
        # Important Files And Flows

        ## Entrypoints

        {ListOrNone(scan.MainEntrypoints.Select(e => $"- `{e}`"))}

        ## Imported Source Paths

        {ListOrNone((doc?.SourcePaths ?? []).Select(p => $"- `{p.OriginalPath}` -> `{p.NormalizedPath}` ({(p.Exists ? "found" : "missing")})"))}

        ## Rules And Mindset

        {ListOrNone((doc?.Rules ?? []).Select(r => $"- {r.Text}"))}

        ## Local API Endpoints

        {ListOrNone(routes.Select(r => $"- {r.HttpMethod} `{r.RoutePattern}` in `{r.RelativePath}:{r.LineNumber}`"))}

        ## Service Registrations

        {ListOrNone(services.Select(s => $"- {s.RegistrationMethod} `{s.ServiceType}` -> `{s.ImplementationType ?? "self"}` in `{s.RelativePath}:{s.LineNumber}`"))}

        ## Background Workers

        {ListOrNone(workers.Select(w => $"- `{w.ClassName}` ({w.BaseTypeOrInterface}) in `{w.RelativePath}:{w.LineNumber}`"))}

        ## Detected Flows

        {ListOrNone(flows.Select(f => $"- {f.Name} ({f.FlowType}, {f.Confidence})"))}

        ## Mermaid Flow Diagrams

        {string.Join(Environment.NewLine + Environment.NewLine, flows.Where(f => f.Confidence == FlowConfidence.High).Take(3).Select(f => $"### {f.Name}{Environment.NewLine}{Environment.NewLine}```mermaid{Environment.NewLine}{(diagrams?.Generate(f) ?? "flowchart TD")}{Environment.NewLine}```"))}
        """;

    private static string Configuration(ProjectScanResult scan, IReadOnlyList<ConfigurationUsage> usages)
        => $"""
        # Configuration And Secrets

        ## Config Files

        {ListOrNone(scan.ConfigFiles.Select(c => $"- `{c}`"))}

        ## Docker Files

        {ListOrNone(scan.DockerFiles.Select(d => $"- `{d}`"))}

        ## Secret Handling

        - Do not store secrets in `.ai-context`, recipe files, or Localite.
        - Treat configuration docs as references only; redact sensitive values.
        - Do not send secret values to AI tools.

        ## Configuration Usages

        {ListOrNone(usages.Select(u => $"- {u.UsageKind} `{u.KeyOrSection}` in `{u.RelativePath}:{u.LineNumber}`"))}
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

    private static string Verification(ProjectInstructionDocument? doc, IReadOnlyList<TestInfo> tests)
        => $"""
        # Verification Checklist

        {ListOrNone((doc?.VerificationItems ?? []).Select(v => $"- {(v.IsCompleted ? "[x]" : "[ ]")} {v.Text}"))}

        ## Default Checks

        - Build the solution.
        - Run relevant tests.
        - Re-run SourceWorker scan after meaningful changes.
        - `dotnet build SourceWorker.slnx`
        - `dotnet test SourceWorker.slnx --no-build`
        - `dotnet run --project src/services/SourceWorker.Cli -- scan <path>`
        - `dotnet run --project src/services/SourceWorker.Cli -- doctor <path>`
        - `dotnet run --project src/services/SourceWorker.Cli -- context-status <path>`
        - `dotnet run --project src/services/SourceWorker.Cli -- artifacts <path>`
        - `dotnet run --project src/services/SourceWorker.Cli -- cleanup-preview <path>`
        - `dotnet run --project src/services/SourceWorker.Cli -- storage-info`

        ## Detected Tests

        {ListOrNone(tests.Take(30).Select(t => $"- `{t.ClassName}.{t.MethodName}` in `{t.RelativePath}:{t.LineNumber}`"))}
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

    private static string Slug(string value)
        => string.Concat(value.ToLowerInvariant().Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')).Trim('-');
}
