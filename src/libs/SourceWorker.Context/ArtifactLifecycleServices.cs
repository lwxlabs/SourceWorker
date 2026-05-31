using SourceWorker.Application;
using SourceWorker.Domain;

namespace SourceWorker.Context;

public sealed class GeneratedArtifactLifecycleService(IGeneratedArtifactRepository artifacts) : IGeneratedArtifactLifecycleService, IArtifactStatusService
{
    private static readonly HashSet<string> CurrentBackendDocs = new(StringComparer.OrdinalIgnoreCase)
    {
        "instructions/Result/Backend/README.md",
        "instructions/Result/Backend/01-project-structure.md",
        "instructions/Result/Backend/02-architecture.md",
        "instructions/Result/Backend/03-important-files-and-flows.md",
        "instructions/Result/Backend/04-configuration-and-secrets.md",
        "instructions/Result/Backend/05-tasks-and-risks.md",
        "instructions/Result/Backend/06-verification-checklist.md"
    };

    private static readonly HashSet<string> DeprecatedBackendDocs = new(StringComparer.OrdinalIgnoreCase)
    {
        "instructions/Result/Backend/03-workers-and-background-jobs.md",
        "instructions/Result/Backend/03-workers-and-services.md",
        "instructions/Result/Backend/04-configuration.md",
        "instructions/Result/Backend/01-api-architecture.md",
        "instructions/Result/Backend/02-project-structure.md"
    };

    public async Task<ArtifactLifecycleReport> AnalyzeAsync(Project project, CancellationToken cancellationToken)
    {
        var tracked = (await artifacts.GetByProjectAsync(project.Id, cancellationToken)).ToList();
        var missing = new List<string>();
        var manual = new List<string>();
        var stale = new List<string>();
        var deprecated = new List<string>();
        var orphaned = new List<string>();

        foreach (var artifact in tracked)
        {
            if (!File.Exists(artifact.AbsolutePath))
            {
                missing.Add(artifact.RelativePath);
                await artifacts.UpsertAsync(artifact with { Status = GeneratedArtifactStatus.Missing }, cancellationToken);
                continue;
            }

            var currentHash = MarkdownContextWriter.Hash(StripMetadata(await File.ReadAllTextAsync(artifact.AbsolutePath, cancellationToken)));
            if (!currentHash.Equals(artifact.ContentHash, StringComparison.OrdinalIgnoreCase))
            {
                manual.Add(artifact.RelativePath);
                await artifacts.UpsertAsync(artifact with { Status = GeneratedArtifactStatus.ManuallyModified }, cancellationToken);
            }
            else if (artifact.Status == GeneratedArtifactStatus.Stale)
            {
                stale.Add(artifact.RelativePath);
            }
        }

        var backendRoot = Path.Combine(project.RootPath, "instructions", "Result", "Backend");
        if (Directory.Exists(backendRoot))
        {
            foreach (var file in Directory.EnumerateFiles(backendRoot, "*.md", SearchOption.TopDirectoryOnly))
            {
                var relative = Path.GetRelativePath(project.RootPath, file).Replace('\\', '/');
                var text = await File.ReadAllTextAsync(file, cancellationToken);
                var generated = text.StartsWith("<!--", StringComparison.Ordinal) && text.Contains("GeneratedBy: SourceWorker", StringComparison.Ordinal);
                if (!generated) continue;
                if (DeprecatedBackendDocs.Contains(relative))
                {
                    deprecated.Add(relative);
                }
                else if (!CurrentBackendDocs.Contains(relative))
                {
                    orphaned.Add(relative);
                }
            }
        }

        return new ArtifactLifecycleReport(project.Id, tracked, missing, manual, stale, orphaned, deprecated, missing.Count + manual.Count + stale.Count + orphaned.Count + deprecated.Count > 0);
    }

    public Task<ArtifactLifecycleReport> GetArtifactsAsync(Project project, CancellationToken cancellationToken) => AnalyzeAsync(project, cancellationToken);

    internal static string StripMetadata(string content)
    {
        if (!content.StartsWith("<!--", StringComparison.Ordinal)) return content;
        var end = content.IndexOf("-->", StringComparison.Ordinal);
        return end < 0 ? content : content[(end + 3)..].TrimStart();
    }
}

public sealed class GeneratedArtifactCleanupService(IGeneratedArtifactLifecycleService lifecycle) : IGeneratedArtifactCleanupService
{
    public async Task<CleanupPreviewReport> PreviewAsync(Project project, CancellationToken cancellationToken)
    {
        var report = await lifecycle.AnalyzeAsync(project, cancellationToken);
        var safe = report.OrphanedArtifacts.Concat(report.DeprecatedArtifacts).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        return new CleanupPreviewReport(project.Id, safe, report.MissingArtifacts, report.StaleArtifacts, report.ManuallyModifiedArtifacts.Select(f => $"Manual edit preserved: {f}").ToArray());
    }

    public async Task<CleanupResult> CleanupAsync(Project project, bool dryRun, bool force, CancellationToken cancellationToken)
    {
        var preview = await PreviewAsync(project, cancellationToken);
        var deleted = new List<string>();
        var skipped = new List<string>();
        var warnings = preview.Warnings.ToList();

        foreach (var relative in preview.SafeDeleteFiles)
        {
            var fullPath = Path.Combine(project.RootPath, relative.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(fullPath))
            {
                skipped.Add(relative);
                continue;
            }

            var text = await File.ReadAllTextAsync(fullPath, cancellationToken);
            var generated = text.StartsWith("<!--", StringComparison.Ordinal) && text.Contains("GeneratedBy: SourceWorker", StringComparison.Ordinal);
            if (!generated)
            {
                skipped.Add(relative);
                warnings.Add($"Skipped non-generated file: {relative}");
                continue;
            }

            if (!dryRun)
            {
                File.Delete(fullPath);
            }
            deleted.Add(relative);
        }

        return new CleanupResult(project.Id, dryRun, deleted, skipped, warnings);
    }
}
