using System.Text;
using SourceWorker.Application;
using SourceWorker.Domain;

namespace SourceWorker.Context;

public sealed class OutputProfileService : IOutputProfileService, IOutputRoutingService
{
    public static readonly string[] BackendDocsFiles =
    [
        "00-project-index.md",
        "README.md",
        "01-project-structure.md",
        "02-architecture.md",
        "03-important-files-and-flows.md",
        "04-configuration-and-secrets.md",
        "05-tasks-and-risks.md",
        "06-verification-checklist.md",
        "08-code-snippets.md",
        "09-reading-guide.md"
    ];

    public Task<IReadOnlyList<OutputProfile>> GetProfilesAsync(Project project, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<OutputProfile>>(
        [
            new OutputProfile(
                "backend-docs-compact",
                "backend-docs-compact",
                [
                    new OutputDestination("internal-backend-docs", OutputDestinationType.InternalContextDocs, ".ai-context/docs/backend", true),
                    new OutputDestination("external-backend-docs", OutputDestinationType.ExternalMarkdownFolder, "instructions/Result/Backend", false)
                ],
                BackendDocsFiles)
        ]);

    public async Task<IReadOnlyList<OutputRoute>> GetRoutesAsync(Project project, string recipeId, CancellationToken cancellationToken)
        => (await GetProfilesAsync(project, cancellationToken))
            .Where(profile => profile.RecipeId.Equals(recipeId, StringComparison.OrdinalIgnoreCase))
            .SelectMany(profile => profile.Destinations.Select(destination => new OutputRoute(recipeId, destination, profile.GeneratedFiles)))
            .ToArray();
}

public sealed class GeneratedOutputService(IOutputProfileService profiles, IGeneratedArtifactLifecycleService lifecycle) : IGeneratedOutputService
{
    public async Task<GeneratedOutputSet> GetOutputSetAsync(Project project, string recipeId, CancellationToken cancellationToken)
    {
        var profile = (await profiles.GetProfilesAsync(project, cancellationToken)).First(p => p.RecipeId == recipeId);
        var report = await lifecycle.AnalyzeAsync(project, cancellationToken);
        var files = profile.Destinations
            .SelectMany(destination => profile.GeneratedFiles.Select(file =>
            {
                var relative = Combine(destination.Path, file);
                var artifact = report.Artifacts.FirstOrDefault(a => a.RelativePath.Equals(relative, StringComparison.OrdinalIgnoreCase));
                var exists = File.Exists(Path.Combine(project.RootPath, relative));
                var type = destination.Primary ? GeneratedArtifactType.InternalGeneratedDoc : GeneratedArtifactType.ExternalExportedDoc;
                return new GeneratedOutputFile(relative, destination.Id, type, artifact?.Status ?? (exists ? GeneratedArtifactStatus.Generated : GeneratedArtifactStatus.Missing), exists);
            }))
            .ToArray();
        return new GeneratedOutputSet(recipeId, profile.Destinations, files, []);
    }

    private static string Combine(string left, string right) => $"{left.TrimEnd('/', '\\')}/{right}".Replace('\\', '/');
}

public sealed class ExportProfileService(IGeneratedArtifactRepository artifacts) : IExportProfileService
{
    public async Task<ExportProfile> WriteBackendDocsProfileAsync(Project project, Guid? scanRunId, IReadOnlyList<GeneratedArtifact> generatedArtifacts, CancellationToken cancellationToken)
    {
        var profile = new ExportProfile(
            "backend-docs-compact",
            "backend-docs-compact",
            ".ai-context/docs/backend",
            "instructions/Result/Backend",
            "MarkdownFolder",
            "mirror",
            OutputProfileService.BackendDocsFiles,
            DateTimeOffset.UtcNow,
            scanRunId,
            generatedArtifacts.Select(a => a.Id).ToArray(),
            [],
            "Do not delete manually modified files; mirror generated internal docs to external target.");
        var relative = ".ai-context/exports/backend-docs-compact.yaml";
        var absolute = Path.Combine(project.RootPath, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
        var yaml = ToYaml(profile);
        await File.WriteAllTextAsync(absolute, yaml, cancellationToken);
        await artifacts.UpsertAsync(new GeneratedArtifact(Guid.NewGuid(), project.Id, scanRunId, profile.RecipeId, relative, absolute, GeneratedArtifactType.ExportProfile, Hash(yaml), DateTimeOffset.UtcNow, Hash(yaml), GeneratedArtifactStatus.Generated, "SourceWorker.ExportProfile", null), cancellationToken);
        return profile;
    }

    public Task<IReadOnlyList<ExportProfile>> ListAsync(Project project, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<ExportProfile>>(File.Exists(Path.Combine(project.RootPath, ".ai-context", "exports", "backend-docs-compact.yaml"))
            ? [ReadBackendProfile(project)]
            : []);

    public Task<ExportProfile?> GetAsync(Project project, string exportProfileId, CancellationToken cancellationToken)
        => Task.FromResult<ExportProfile?>(exportProfileId.Equals("backend-docs-compact", StringComparison.OrdinalIgnoreCase) ? ReadBackendProfile(project) : null);

    public static string ToYaml(ExportProfile profile)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"id: {profile.Id}");
        builder.AppendLine($"recipeId: {profile.RecipeId}");
        builder.AppendLine($"internalSourcePath: {profile.InternalSourcePath}");
        builder.AppendLine($"externalOutputPath: {profile.ExternalOutputPath}");
        builder.AppendLine($"targetPlatform: {profile.TargetPlatform}");
        builder.AppendLine($"publishMode: {profile.PublishMode}");
        builder.AppendLine($"lastExportUtc: \"{profile.LastExportUtc:O}\"");
        builder.AppendLine($"lastExportScanRunId: \"{profile.LastExportScanRunId}\"");
        builder.AppendLine("generatedFiles:");
        foreach (var file in profile.GeneratedFiles) builder.AppendLine($"  - {file}");
        builder.AppendLine("warnings: []");
        builder.AppendLine($"cleanupBehavior: \"{profile.CleanupBehavior}\"");
        return builder.ToString();
    }

    private static ExportProfile ReadBackendProfile(Project project)
        => new("backend-docs-compact", "backend-docs-compact", ".ai-context/docs/backend", "instructions/Result/Backend", "MarkdownFolder", "mirror", OutputProfileService.BackendDocsFiles, File.GetLastWriteTimeUtc(Path.Combine(project.RootPath, ".ai-context", "exports", "backend-docs-compact.yaml")), null, [], [], "Do not delete manually modified files; mirror generated internal docs to external target.");

    private static string Hash(string content) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant();
}

public sealed class ExportRunner(IExportProfileService profiles) : IExportRunner
{
    public async Task<ExportRun> RunAsync(Project project, string exportProfileId, CancellationToken cancellationToken)
    {
        var profile = await profiles.GetAsync(project, exportProfileId, cancellationToken) ?? throw new InvalidOperationException($"Export profile '{exportProfileId}' was not found.");
        var sourceRoot = Path.GetFullPath(Path.Combine(project.RootPath, profile.InternalSourcePath));
        var outputRoot = Path.GetFullPath(Path.Combine(project.RootPath, profile.ExternalOutputPath));
        if (!sourceRoot.StartsWith(Path.GetFullPath(project.RootPath), StringComparison.OrdinalIgnoreCase) || !outputRoot.StartsWith(Path.GetFullPath(project.RootPath), StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Export paths must stay inside the project root.");
        Directory.CreateDirectory(outputRoot);
        var exported = new List<string>();
        foreach (var file in profile.GeneratedFiles)
        {
            var source = Path.Combine(sourceRoot, file);
            var target = Path.Combine(outputRoot, file);
            if (!File.Exists(source)) continue;
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(source, target, overwrite: true);
            exported.Add(Path.GetRelativePath(project.RootPath, target).Replace('\\', '/'));
        }
        var now = DateTimeOffset.UtcNow;
        return new ExportRun(Guid.NewGuid().ToString("n"), profile.Id, project.Id, now, now, exported, []);
    }
}

public sealed class ContextPackService(IGeneratedArtifactRepository artifacts) : IContextPackService, IContextPackWriter, IContextPackReader, IContextPackValidator
{
    public async Task<IReadOnlyList<ContextPackManifest>> WriteDefaultPacksAsync(Project project, Guid? scanRunId, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.Combine(project.RootPath, ".ai-context", "packs"));
        var packs = DefaultPacks(scanRunId).ToArray();
        var manifests = new List<ContextPackManifest>();
        foreach (var (pack, relative) in packs)
        {
            var manifest = await ValidateAsync(project, pack, relative, cancellationToken);
            var yaml = ToYaml(manifest.Pack, manifest.MissingFiles);
            var absolute = Path.Combine(project.RootPath, relative);
            await File.WriteAllTextAsync(absolute, yaml, cancellationToken);
            await artifacts.UpsertAsync(new GeneratedArtifact(Guid.NewGuid(), project.Id, scanRunId, null, relative, absolute, GeneratedArtifactType.ContextPackManifest, Hash(yaml), DateTimeOffset.UtcNow, Hash(yaml), GeneratedArtifactStatus.Generated, "SourceWorker.ContextPack", null), cancellationToken);
            manifests.Add(manifest);
        }
        return manifests;
    }

    public async Task<IReadOnlyList<ContextPackManifest>> ListAsync(Project project, CancellationToken cancellationToken)
    {
        var manifests = new List<ContextPackManifest>();
        foreach (var (pack, relative) in DefaultPacks(null))
        {
            manifests.Add(await ValidateAsync(project, pack, relative, cancellationToken));
        }
        return manifests;
    }

    public async Task<ContextPackManifest?> GetAsync(Project project, string packId, CancellationToken cancellationToken)
    {
        var item = DefaultPacks(null).FirstOrDefault(p => p.Pack.Id.Equals(packId, StringComparison.OrdinalIgnoreCase));
        return item.Pack is null ? null : await ValidateAsync(project, item.Pack, item.RelativePath, cancellationToken);
    }

    public async Task<string> ReadPackContentAsync(Project project, string packId, CancellationToken cancellationToken)
    {
        var manifest = await GetAsync(project, packId, cancellationToken) ?? throw new InvalidOperationException($"Pack '{packId}' was not found.");
        var builder = new StringBuilder();
        builder.AppendLine($"# {manifest.Pack.Name}");
        builder.AppendLine();
        builder.AppendLine(manifest.Pack.Description);
        builder.AppendLine();
        foreach (var item in manifest.Pack.Items)
        {
            var path = Path.GetFullPath(Path.Combine(project.RootPath, item.RelativePath));
            if (!path.StartsWith(Path.GetFullPath(project.RootPath), StringComparison.OrdinalIgnoreCase) || !File.Exists(path)) continue;
            builder.AppendLine();
            builder.AppendLine($"---");
            builder.AppendLine($"Source: `{item.RelativePath}`");
            builder.AppendLine($"---");
            builder.AppendLine();
            builder.AppendLine(await File.ReadAllTextAsync(path, cancellationToken));
        }
        return builder.ToString();
    }

    public Task<ContextPackManifest> ValidateAsync(Project project, ContextPack pack, string relativePath, CancellationToken cancellationToken)
    {
        var missing = pack.Items.Where(item => item.Required && !File.Exists(Path.Combine(project.RootPath, item.RelativePath))).Select(item => item.RelativePath).ToArray();
        return Task.FromResult(new ContextPackManifest(pack, relativePath, missing, missing.Length == 0));
    }

    private static IEnumerable<(ContextPack Pack, string RelativePath)> DefaultPacks(Guid? scanRunId)
    {
        var now = DateTimeOffset.UtcNow;
        yield return (Pack("ai-handoff", "AI Handoff Pack", "Concise AI coding assistant handoff.", "assistant", "Start work safely from current project context.", [".ai-context/ai-handoff.md", ".ai-context/project.md", ".ai-context/current-state.md"], scanRunId, now), ".ai-context/packs/ai-handoff.pack.yaml");
        yield return (Pack("backend-docs", "Backend Docs Pack", "Understand backend architecture, flows, configuration, tasks, and verification.", "developer", "Backend developer handoff.", OutputProfileService.BackendDocsFiles.Select(f => $".ai-context/docs/backend/{f}").ToArray(), scanRunId, now), ".ai-context/packs/backend-docs.pack.yaml");
        yield return (Pack("onboarding", "Project Onboarding Pack", "Project structure, architecture, tasks, and backend docs overview.", "developer", "Onboard quickly into the repository.", [".ai-context/project.md", ".ai-context/architecture.md", ".ai-context/docs/backend/README.md"], scanRunId, now), ".ai-context/packs/onboarding.pack.yaml");
        yield return (Pack("current-branch", "Current Branch Pack", "Current Git branch state and latest scan.", "developer", "Understand the current branch before editing.", [".ai-context/branches/current.md", ".ai-context/scans/latest-scan.md", ".ai-context/current-state.md"], scanRunId, now), ".ai-context/packs/current-branch.pack.yaml");
        yield return (Pack("dogfood-feedback", "Dogfood Feedback Pack", "Latest dogfood feedback, open friction issues, diagnostics, and current project handoff notes.", "product-builder", "Use SourceWorker dogfood feedback to prioritize the next local-engine improvement pass.", [".ai-context/dogfood/issues.md", ".ai-context/current-state.md", ".ai-context/ai-handoff.md"], scanRunId, now), ".ai-context/packs/dogfood-feedback.pack.yaml");
        yield return (Pack("ai-explanation", "AI Project Explanation", "Human-readable AI-generated project explanation plus supporting handoff and architecture context.", "developer", "Use after generating 07-ai-explanation.md for onboarding, documentation, and agent handoff.", [".ai-context/docs/backend/07-ai-explanation.md", ".ai-context/agent-handoff.md", ".ai-context/docs/backend/02-architecture.md", ".ai-context/docs/backend/03-important-files-and-flows.md"], scanRunId, now), ".ai-context/packs/ai-explanation.pack.yaml");
    }

    private static ContextPack Pack(string id, string name, string description, string audience, string purpose, IReadOnlyList<string> files, Guid? scanRunId, DateTimeOffset now)
        => new(id, name, description, audience, purpose, files.Select(f => new ContextPackItem(f, Path.GetExtension(f).Trim('.'), true)).ToArray(), [], "compact", 0, "unknown", scanRunId, now);

    private static string ToYaml(ContextPack pack, IReadOnlyList<string> missing)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"id: {pack.Id}");
        builder.AppendLine($"name: {pack.Name}");
        builder.AppendLine($"description: {pack.Description}");
        builder.AppendLine($"audience: {pack.Audience}");
        builder.AppendLine($"purpose: {pack.Purpose}");
        builder.AppendLine($"maxRecommendedUse: {pack.MaxRecommendedUse}");
        builder.AppendLine($"tokenEstimate: {pack.TokenEstimate}");
        builder.AppendLine($"freshnessStatus: {(missing.Count == 0 ? "fresh" : "missing-files")}");
        builder.AppendLine($"sourceScanRunId: \"{pack.SourceScanRunId}\"");
        builder.AppendLine($"generatedUtc: \"{pack.GeneratedUtc:O}\"");
        builder.AppendLine("files:");
        foreach (var item in pack.Items) builder.AppendLine($"  - {item.RelativePath}");
        builder.AppendLine("missingFiles:");
        foreach (var file in missing) builder.AppendLine($"  - {file}");
        return builder.ToString();
    }

    private static string Hash(string content) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant();
}
