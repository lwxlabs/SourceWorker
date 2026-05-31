using SourceWorker.Application;
using SourceWorker.Domain;

namespace SourceWorker.Context;

public sealed class ContextFreshnessService(IGeneratedArtifactRepository artifacts, IProjectInstructionRepository instructions, ISourcePathValidator validator) : IContextFreshnessService, IContextStatusService
{
    public async Task<ContextFreshnessReport> CheckAsync(Project project, CancellationToken cancellationToken)
    {
        var all = await artifacts.GetByProjectAsync(project.Id, cancellationToken);
        var missing = new List<string>();
        var manual = new List<string>();
        var stale = new List<string>();

        foreach (var artifact in all)
        {
            if (!File.Exists(artifact.AbsolutePath))
            {
                missing.Add(artifact.RelativePath);
                continue;
            }

            var currentHash = MarkdownContextWriter.Hash(StripMetadata(await File.ReadAllTextAsync(artifact.AbsolutePath, cancellationToken)));
            if (!currentHash.Equals(artifact.ContentHash, StringComparison.OrdinalIgnoreCase))
            {
                manual.Add(artifact.RelativePath);
            }
            else if (artifact.Status == GeneratedArtifactStatus.Stale)
            {
                stale.Add(artifact.RelativePath);
            }
        }

        var doc = await instructions.GetLatestAsync(project.Id, cancellationToken);
        var validationWarnings = doc is null ? 0 : validator.Validate(project.RootPath, doc.SourcePaths.Select(p => p.OriginalPath)).Count(p => p.WarningMessage is not null);
        var score = Math.Clamp(100 - (missing.Count * 20) - (manual.Count * 15) - (stale.Count * 10) - (validationWarnings * 5), 0, 100);
        var actions = new List<string>();
        if (missing.Count > 0) actions.Add($"run sourceworker scan \"{project.RootPath}\"");
        if (stale.Count > 0) actions.Add($"run sourceworker run-recipe \"{project.RootPath}\" backend-docs-compact");
        if (manual.Count > 0) actions.Add("review manually modified generated files");
        if (actions.Count == 0) actions.Add("no action required");

        return new ContextFreshnessReport(project.Id, missing.Count > 0 || manual.Count > 0 || stale.Count > 0, stale, missing, manual, [], score, actions);
    }

    public Task<ContextFreshnessReport> GetStatusAsync(Project project, CancellationToken cancellationToken)
        => CheckAsync(project, cancellationToken);

    private static string StripMetadata(string content)
    {
        if (!content.StartsWith("<!--", StringComparison.Ordinal)) return content;
        var end = content.IndexOf("-->", StringComparison.Ordinal);
        return end < 0 ? content : content[(end + 3)..].TrimStart();
    }
}

public sealed class ChangeSetService(IIndexedFileRepository indexedFiles, IScanRunRepository scanRuns) : IChangeSetService
{
    public async Task<ChangeSetReport> GetChangesAsync(Project project, CancellationToken cancellationToken)
    {
        var latest = await scanRuns.GetLatestAsync(project.Id, cancellationToken);
        if (latest is null) return new ChangeSetReport(project.Id, null, [], [], [], 0, 0);
        var files = await indexedFiles.GetByScanRunAsync(latest.Id, cancellationToken);
        return new ChangeSetReport(
            project.Id,
            latest.Id,
            files.Where(f => f.ChangeKind == FileChangeKind.New).Select(f => f.RelativePath).ToArray(),
            files.Where(f => f.ChangeKind == FileChangeKind.Changed).Select(f => f.RelativePath).ToArray(),
            files.Where(f => f.ChangeKind == FileChangeKind.Deleted).Select(f => f.RelativePath).ToArray(),
            files.Count(f => f.ChangeKind == FileChangeKind.Unchanged),
            files.Count(f => f.WasIgnored));
    }
}
