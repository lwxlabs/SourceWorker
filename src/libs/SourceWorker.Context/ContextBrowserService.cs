using SourceWorker.Application;
using SourceWorker.Domain;

namespace SourceWorker.Context;

public sealed class ContextBrowserService(IRecipeRepository recipes, ICodeFlowRepository flows, IRouteInfoRepository routes, IGeneratedArtifactRepository artifacts) : IContextBrowserService, IContextSearchService
{
    public async Task<ContextBrowserSummary> GetSummaryAsync(Project project, CancellationToken cancellationToken)
        => new(project.Id, await ListFilesAsync(project, showTechnical: false, showExports: false, cancellationToken), await recipes.ListAsync(project, cancellationToken), await flows.GetByProjectAsync(project.Id, cancellationToken), await routes.GetByProjectAsync(project.Id, cancellationToken), [], (await artifacts.GetByProjectAsync(project.Id, cancellationToken)).Where(a => a.Status != GeneratedArtifactStatus.Generated).ToArray());

    public Task<IReadOnlyList<ContextBrowserFile>> ListFilesAsync(Project project, bool showTechnical, bool showExports, CancellationToken cancellationToken)
    {
        var roots = AllowedRoots(project, showExports);
        var files = roots.Where(Directory.Exists)
            .SelectMany(root => Directory.EnumerateFiles(root, "*.md", SearchOption.AllDirectories).Concat(Directory.EnumerateFiles(root, "*.yaml", SearchOption.AllDirectories)).Concat(Directory.EnumerateFiles(root, "*.mmd", SearchOption.AllDirectories)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(path => new FileInfo(path))
            .Select(info => new ContextBrowserFile(Path.GetRelativePath(project.RootPath, info.FullName).Replace('\\', '/'), info.FullName, Group(Path.GetRelativePath(project.RootPath, info.FullName).Replace('\\', '/')), info.Length, info.LastWriteTimeUtc))
            .Where(file => showTechnical || !IsTechnical(file.RelativePath))
            .OrderBy(f => f.RelativePath)
            .ToArray();
        return Task.FromResult<IReadOnlyList<ContextBrowserFile>>(files);
    }

    public async Task<string> ReadFileAsync(Project project, string relativePath, CancellationToken cancellationToken)
    {
        var normalized = relativePath.Replace('\\', '/').TrimStart('/');
        var fullPath = Path.GetFullPath(Path.Combine(project.RootPath, normalized));
        var allowed = AllowedRoots(project, showExports: true).Any(root => fullPath.StartsWith(Path.GetFullPath(root), StringComparison.OrdinalIgnoreCase));
        if (!allowed || !File.Exists(fullPath)) throw new InvalidOperationException("Context file path is not allowed.");
        return await File.ReadAllTextAsync(fullPath, cancellationToken);
    }

    public async Task<ContextBrowserQueryResult> SearchAsync(Project project, string query, CancellationToken cancellationToken)
    {
        var hits = new List<ContextBrowserSearchHit>();
        foreach (var file in await ListFilesAsync(project, showTechnical: false, showExports: false, cancellationToken))
        {
            var lines = await File.ReadAllLinesAsync(file.AbsolutePath, cancellationToken);
            for (var i = 0; i < lines.Length; i++)
                if (lines[i].Contains(query, StringComparison.OrdinalIgnoreCase))
                    hits.Add(new ContextBrowserSearchHit(file.RelativePath, i + 1, lines[i].Trim()));
        }
        return new ContextBrowserQueryResult(query, hits.Take(100).ToArray());
    }

    private static string[] AllowedRoots(Project project, bool showExports)
        => showExports
            ? [Path.Combine(project.RootPath, ".ai-context"), Path.Combine(project.RootPath, "instructions", "Result", "Backend")]
            : [Path.Combine(project.RootPath, ".ai-context")];

    private static bool IsTechnical(string relativePath)
        => relativePath.Equals(".ai-context/index.yaml", StringComparison.OrdinalIgnoreCase) ||
           relativePath.StartsWith(".ai-context/recipes/", StringComparison.OrdinalIgnoreCase) ||
           relativePath.StartsWith(".ai-context/exports/", StringComparison.OrdinalIgnoreCase) ||
           relativePath.StartsWith(".ai-context/scans/", StringComparison.OrdinalIgnoreCase) ||
           relativePath.StartsWith(".ai-context/branches/", StringComparison.OrdinalIgnoreCase) ||
           relativePath.StartsWith(".ai-context/diagrams/", StringComparison.OrdinalIgnoreCase) ||
           relativePath.StartsWith("instructions/Result/Backend/", StringComparison.OrdinalIgnoreCase);

    private static string Group(string relativePath)
        => relativePath switch
        {
            ".ai-context/agent-handoff.md" => "Agent Handoff",
            var path when path.StartsWith(".ai-context/instructions/", StringComparison.OrdinalIgnoreCase) => "Instructions",
            var path when path.StartsWith(".ai-context/docs/", StringComparison.OrdinalIgnoreCase) => "Generated Docs",
            var path when path.StartsWith(".ai-context/recipes/", StringComparison.OrdinalIgnoreCase) => "Recipes",
            var path when path.StartsWith(".ai-context/packs/", StringComparison.OrdinalIgnoreCase) => "Packs",
            var path when path.StartsWith(".ai-context/exports/", StringComparison.OrdinalIgnoreCase) => "Exports",
            var path when path.StartsWith(".ai-context/diagrams/", StringComparison.OrdinalIgnoreCase) => "Diagrams",
            var path when path.StartsWith("instructions/Result/Backend/", StringComparison.OrdinalIgnoreCase) => "External Outputs",
            _ => "Root Context"
        };
}

public sealed class AgentHandoffService : IAgentHandoffService
{
    public async Task<AgentHandoff> GetAsync(Project project, CancellationToken cancellationToken)
    {
        var relativePath = ".ai-context/agent-handoff.md";
        var absolutePath = Path.Combine(project.RootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
        var markdown = File.Exists(absolutePath)
            ? await File.ReadAllTextAsync(absolutePath, cancellationToken)
            : $"""
              # Agent Handoff

              No generated handoff exists yet for `{project.Name}`.

              Run a scan to generate `.ai-context/agent-handoff.md`.
              """;

        return new AgentHandoff(project.Id, relativePath, markdown, "ai-handoff", Templates);
    }

    private static readonly AgentHandoffTemplate[] Templates =
    [
        new("generic", "Generic AI Agent", "You are working in this repository. Use the context below. Do not guess method names. Inspect files before editing. Keep architecture intact."),
        new("codex", "Codex", "You are working in this repository. Use the context below. Do not guess method names. Inspect files before editing. Keep architecture intact. Run targeted tests only."),
        new("junie", "Junie", "Use this as project context. Focus on understanding the existing architecture before making changes. Do not run full tests unless shared contracts or core behavior changed."),
        new("cursor", "Cursor", "Use this repository handoff as context. Prefer existing project structure and inspect referenced files before edits."),
        new("chatgpt", "ChatGPT", "Use this as project context for planning, review, or explanation. Do not invent code details that are not present in the handoff."),
        new("claude-code", "Claude Code", "Use this handoff to understand the repository before making changes. Preserve clean architecture and verify with targeted tests.")
    ];
}

public sealed class ScanCompletenessService(
    IScanRunRepository scanRuns,
    IIndexedFileRepository indexedFiles,
    IGeneratedArtifactRepository artifacts,
    ICodeSymbolRepository symbols,
    ICodeFlowRepository flows,
    IRouteInfoRepository routes,
    IServiceRegistrationRepository services,
    IConfigurationUsageRepository configs,
    ITestInfoRepository tests) : IScanCompletenessService
{
    public async Task<ScanCompletenessReport> GetAsync(Project project, CancellationToken cancellationToken)
    {
        var latest = await scanRuns.GetLatestAsync(project.Id, cancellationToken);
        if (latest is null)
        {
            return new ScanCompletenessReport(project.Id, null, 0, [Stage("Latest scan", ScanStageStatus.NotStarted, 0, "Run a scan to create context.")], [new("no-scan", "No scan run exists for this project.")], "No scan exists yet.");
        }

        var files = await indexedFiles.GetByScanRunAsync(latest.Id, cancellationToken);
        var projectArtifacts = await artifacts.GetByProjectAsync(project.Id, cancellationToken);
        var codeSymbols = await symbols.GetByProjectAsync(project.Id, cancellationToken);
        var codeFlows = await flows.GetByProjectAsync(project.Id, cancellationToken);
        var routeInfos = await routes.GetByProjectAsync(project.Id, cancellationToken);
        var serviceRegs = await services.GetByProjectAsync(project.Id, cancellationToken);
        var configUsages = await configs.GetByProjectAsync(project.Id, cancellationToken);
        var testInfos = await tests.GetByProjectAsync(project.Id, cancellationToken);
        var csFiles = files.Where(f => f.Extension.Equals(".cs", StringComparison.OrdinalIgnoreCase) && !f.WasIgnored).ToArray();
        var isLibraryProfile = routeInfos.Count == 0 && testInfos.Count > 0;
        var stages = new List<ScanStageResult>
        {
            Stage("Project root validation", Directory.Exists(project.RootPath) ? ScanStageStatus.Completed : ScanStageStatus.Failed, Directory.Exists(project.RootPath) ? 1 : 0, Directory.Exists(project.RootPath) ? null : "Project root was not found."),
            Stage("Ignore rules loaded", ScanStageStatus.Completed, File.Exists(Path.Combine(project.RootPath, ".gitignore")) ? 1 : 0, File.Exists(Path.Combine(project.RootPath, ".gitignore")) ? null : "No .gitignore file was found; built-in skip rules were still applied."),
            Stage("Files indexed", files.Count > 0 ? ScanStageStatus.Completed : ScanStageStatus.CompletedWithWarnings, files.Count, files.Count > 0 ? null : "No files were indexed."),
            Stage("Solution files detected", files.Any(f => f.Kind == IndexedFileKind.Solution) ? ScanStageStatus.Completed : ScanStageStatus.CompletedWithWarnings, files.Count(f => f.Kind == IndexedFileKind.Solution), files.Any(f => f.Kind == IndexedFileKind.Solution) ? null : "No solution file was detected."),
            Stage("Project files parsed", files.Any(f => f.Kind == IndexedFileKind.Project) ? ScanStageStatus.Completed : ScanStageStatus.CompletedWithWarnings, files.Count(f => f.Kind == IndexedFileKind.Project), files.Any(f => f.Kind == IndexedFileKind.Project) ? null : "No project files were detected."),
            Stage("Target frameworks detected", files.Any(f => f.Kind == IndexedFileKind.Project) ? ScanStageStatus.Completed : ScanStageStatus.Skipped, files.Count(f => f.Kind == IndexedFileKind.Project), files.Any(f => f.Kind == IndexedFileKind.Project) ? null : "Requires project files."),
            Stage("Package references detected", files.Any(f => f.Kind == IndexedFileKind.Project) ? ScanStageStatus.Completed : ScanStageStatus.Skipped, files.Count(f => f.Kind == IndexedFileKind.Project), files.Any(f => f.Kind == IndexedFileKind.Project) ? null : "Requires project files."),
            Stage("Project references detected", files.Any(f => f.Kind == IndexedFileKind.Project) ? ScanStageStatus.Completed : ScanStageStatus.Skipped, files.Count(f => f.Kind == IndexedFileKind.Project), files.Any(f => f.Kind == IndexedFileKind.Project) ? null : "Requires project files."),
            Stage("Git state detected", ScanStageStatus.Completed, 1),
            Stage("TODO/FIXME extracted", ScanStageStatus.Completed, latest.TodosFound),
            CodeStage("C# symbol scan", csFiles.Length, codeSymbols.Count),
            Stage("Route scan", routeInfos.Count > 0 || csFiles.Length == 0 || isLibraryProfile ? ScanStageStatus.Completed : ScanStageStatus.CompletedWithWarnings, routeInfos.Count, csFiles.Length == 0 || isLibraryProfile ? null : "No routes detected. This may be normal for non-API projects."),
            Stage("Service registration scan", serviceRegs.Count > 0 || csFiles.Length == 0 || isLibraryProfile ? ScanStageStatus.Completed : ScanStageStatus.CompletedWithWarnings, serviceRegs.Count, csFiles.Length == 0 || isLibraryProfile ? null : "No DI registrations detected."),
            Stage("Configuration usage scan", configUsages.Count > 0 || csFiles.Length == 0 || isLibraryProfile ? ScanStageStatus.Completed : ScanStageStatus.CompletedWithWarnings, configUsages.Count, csFiles.Length == 0 || isLibraryProfile ? null : "No configuration usages detected."),
            Stage("Test scan", testInfos.Count > 0 || !files.Any(f => f.RelativePath.Contains("test", StringComparison.OrdinalIgnoreCase)) ? ScanStageStatus.Completed : ScanStageStatus.CompletedWithWarnings, testInfos.Count, testInfos.Count > 0 || !files.Any(f => f.RelativePath.Contains("test", StringComparison.OrdinalIgnoreCase)) ? null : "Test-like files were found but no test methods were detected."),
            Stage("Flow extraction", codeFlows.Count > 0 || csFiles.Length == 0 ? ScanStageStatus.Completed : ScanStageStatus.CompletedWithWarnings, codeFlows.Count, csFiles.Length == 0 ? null : "No high-level flows were extracted."),
            Stage("Context files generated", projectArtifacts.Any(a => a.Type == GeneratedArtifactType.InternalContextFile) ? ScanStageStatus.Completed : ScanStageStatus.CompletedWithWarnings, projectArtifacts.Count(a => a.Type == GeneratedArtifactType.InternalContextFile), "No tracked internal context files found."),
            Stage("Context packs generated", projectArtifacts.Any(a => a.Type == GeneratedArtifactType.ContextPackManifest) ? ScanStageStatus.Completed : ScanStageStatus.CompletedWithWarnings, projectArtifacts.Count(a => a.Type == GeneratedArtifactType.ContextPackManifest), "No tracked context pack manifests found."),
            Stage("Backend docs generated", projectArtifacts.Any(a => a.Type == GeneratedArtifactType.InternalGeneratedDoc) ? ScanStageStatus.Completed : ScanStageStatus.CompletedWithWarnings, projectArtifacts.Count(a => a.Type == GeneratedArtifactType.InternalGeneratedDoc), "No tracked internal backend docs found."),
            Stage("Artifact tracking updated", projectArtifacts.Count > 0 ? ScanStageStatus.Completed : ScanStageStatus.CompletedWithWarnings, projectArtifacts.Count, "No generated artifacts were tracked.")
        };

        var warnings = stages.SelectMany(s => s.Warnings).ToArray();
        var completedScore = stages.Sum(s => s.Status switch
        {
            ScanStageStatus.Completed => 5,
            ScanStageStatus.CompletedWithWarnings => 3,
            ScanStageStatus.Skipped => 2,
            _ => 0
        });
        var score = Math.Clamp((int)Math.Round(completedScore / (double)(stages.Count * 5) * 100), 0, 100);
        var explanation = csFiles.Length > 0 && codeSymbols.Count == 0
            ? $"C# files were found ({csFiles.Length}), but no symbols were extracted. Possible reasons: files were ignored, parser failed, unsupported syntax, scan result was not persisted, or UI loaded stale scan data."
            : codeSymbols.Count > 0
                ? $"C# symbol scan found {codeSymbols.Count} symbols from {csFiles.Length} C# files."
                : "No C# files were found, so code intelligence may be empty.";

        return new ScanCompletenessReport(project.Id, latest.Id, score, stages, warnings, explanation);
    }

    private static ScanStageResult CodeStage(string name, int csFileCount, int symbolCount)
        => csFileCount > 0 && symbolCount == 0
            ? Stage(name, ScanStageStatus.CompletedWithWarnings, 0, $"C# files were found ({csFileCount}), but no symbols were extracted.")
            : Stage(name, ScanStageStatus.Completed, symbolCount);

    private static ScanStageResult Stage(string name, ScanStageStatus status, int count, string? warning = null)
        => new(name, status, count, string.IsNullOrWhiteSpace(warning) ? [] : [new(Slug(name), warning)], null, null, []);

    private static string Slug(string value)
        => string.Concat(value.ToLowerInvariant().Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')).Trim('-');
}
