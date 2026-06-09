using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using SourceWorker.Application;
using SourceWorker.Domain;

namespace SourceWorker.Context;

public sealed class SourceContextBranchService(ISourceContextBranchRepository branches) : ISourceContextBranchService
{
    private const string DefaultBranchName = "main-context";

    public async Task<IReadOnlyList<SourceContextBranch>> GetBranchesAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var existing = await branches.GetByProjectAsync(projectId, cancellationToken);
        return existing.Count == 0 ? [await EnsureDefaultBranchAsync(projectId, cancellationToken)] : existing;
    }

    public Task<SourceContextBranch?> GetBranchAsync(Guid projectId, Guid branchId, CancellationToken cancellationToken)
        => branches.GetAsync(projectId, branchId, cancellationToken);

    public async Task<SourceContextBranch> CreateBranchAsync(Guid projectId, string name, string? description, CancellationToken cancellationToken)
    {
        var normalized = NormalizeName(name);
        if (await branches.GetByNameAsync(projectId, normalized, cancellationToken) is { } existing)
        {
            return existing;
        }

        var branch = new SourceContextBranch(Guid.NewGuid(), projectId, normalized, description, SourceContextBranchKind.UserCreated, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null, null, false, false, false, null, null, null, null);
        return await branches.SaveAsync(branch, cancellationToken);
    }

    public async Task<SourceContextBranch> RenameBranchAsync(Guid projectId, Guid branchId, string name, CancellationToken cancellationToken)
    {
        var branch = await RequireBranch(projectId, branchId, cancellationToken);
        if (branch.IsDefault) return branch;
        return await branches.SaveAsync(branch with { Name = NormalizeName(name), LastSeenUtc = DateTimeOffset.UtcNow }, cancellationToken);
    }

    public async Task<SourceContextBranch> UpdateBranchDescriptionAsync(Guid projectId, Guid branchId, string? description, CancellationToken cancellationToken)
    {
        var branch = await RequireBranch(projectId, branchId, cancellationToken);
        return await branches.SaveAsync(branch with { Description = description, LastSeenUtc = DateTimeOffset.UtcNow }, cancellationToken);
    }

    public async Task<SourceContextBranch> ActivateBranchAsync(Guid projectId, Guid branchId, CancellationToken cancellationToken)
    {
        var all = await GetBranchesAsync(projectId, cancellationToken);
        var target = all.FirstOrDefault(b => b.Id == branchId) ?? throw new InvalidOperationException("Context branch was not found.");
        SourceContextBranch activated = target;
        foreach (var branch in all)
        {
            var next = branch with { IsActive = branch.Id == branchId, LastSeenUtc = branch.Id == branchId ? DateTimeOffset.UtcNow : branch.LastSeenUtc };
            var saved = await branches.SaveAsync(next, cancellationToken);
            if (saved.Id == branchId) activated = saved;
        }

        return activated;
    }

    public async Task<bool> RemoveBranchAsync(Guid projectId, Guid branchId, CancellationToken cancellationToken)
    {
        var all = await GetBranchesAsync(projectId, cancellationToken);
        var branch = all.FirstOrDefault(b => b.Id == branchId);
        if (branch is null || branch.IsDefault || all.Count <= 1) return false;

        await branches.SaveAsync(branch with { IsDeleted = true, IsActive = false, LastSeenUtc = DateTimeOffset.UtcNow }, cancellationToken);
        if (branch.IsActive)
        {
            var replacement = all.FirstOrDefault(b => b.Id != branchId && !b.IsDeleted && b.IsDefault) ?? all.First(b => b.Id != branchId && !b.IsDeleted);
            await ActivateBranchAsync(projectId, replacement.Id, cancellationToken);
        }

        return true;
    }

    public async Task<SourceContextBranch> GetActiveBranchAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var all = await GetBranchesAsync(projectId, cancellationToken);
        return all.FirstOrDefault(b => b.IsActive) ?? await ActivateBranchAsync(projectId, all.First().Id, cancellationToken);
    }

    public async Task<SourceContextBranch> EnsureDefaultBranchAsync(Guid projectId, CancellationToken cancellationToken)
    {
        if (await branches.GetByNameAsync(projectId, DefaultBranchName, cancellationToken) is { } existing)
        {
            return existing;
        }

        var branch = new SourceContextBranch(Guid.NewGuid(), projectId, DefaultBranchName, "Default SourceWorker context branch.", SourceContextBranchKind.Default, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null, null, true, false, true, null, null, null, null);
        return await branches.SaveAsync(branch, cancellationToken);
    }

    private async Task<SourceContextBranch> RequireBranch(Guid projectId, Guid branchId, CancellationToken cancellationToken)
        => await branches.GetAsync(projectId, branchId, cancellationToken) ?? throw new InvalidOperationException("Context branch was not found.");

    private static string NormalizeName(string name)
        => string.IsNullOrWhiteSpace(name) ? DefaultBranchName : name.Trim();
}

public sealed class SourceSnapshotService(
    IProjectRepository projects,
    IScanRunRepository scanRuns,
    IIndexedFileRepository indexedFiles,
    IGeneratedArtifactRepository artifacts,
    ICodeSymbolRepository symbols,
    ICodeFlowRepository flows,
    IRouteInfoRepository routes,
    ITestInfoRepository tests,
    ISourceContextBranchService branches,
    ISourceContextBranchRepository branchRepository,
    ISourceSnapshotRepository snapshots,
    ISourceSnapshotFileRepository snapshotFiles,
    ISourceSnapshotMetricRepository metrics,
    IOptionalGitMetadataService git) : ISourceSnapshotService
{
    public async Task<SourceSnapshot> CreateSnapshotFromScanAsync(Guid projectId, Guid scanRunId, CancellationToken cancellationToken)
    {
        var project = await projects.GetAsync(projectId, cancellationToken) ?? throw new InvalidOperationException("Project was not found.");
        var scan = await scanRuns.GetAsync(scanRunId, cancellationToken) ?? throw new InvalidOperationException("Scan run was not found.");
        var active = await branches.GetActiveBranchAsync(projectId, cancellationToken);
        var existing = await snapshots.GetByScanRunAsync(projectId, scanRunId, cancellationToken);
        var indexed = await indexedFiles.GetByScanRunAsync(scanRunId, cancellationToken);
        var generated = await artifacts.GetByProjectAsync(projectId, cancellationToken);
        var symbolCount = (await symbols.GetByProjectAsync(projectId, cancellationToken)).Count;
        var flowCount = (await flows.GetByProjectAsync(projectId, cancellationToken)).Count;
        var routeCount = (await routes.GetByProjectAsync(projectId, cancellationToken)).Count;
        var testCount = (await tests.GetByProjectAsync(projectId, cancellationToken)).Count;
        var gitMetadata = await git.TryGetGitMetadataAsync(project.RootPath, cancellationToken);

        var files = await BuildFilesAsync(project, existing?.Id ?? Guid.NewGuid(), indexed, generated, cancellationToken);
        var handoffHash = files.FirstOrDefault(f => f.RelativePath.Equals(".ai-context/agent-handoff.md", StringComparison.OrdinalIgnoreCase))?.ContentHash;
        var explanationHash = files.FirstOrDefault(f => f.RelativePath.Equals(".ai-context/docs/backend/07-ai-explanation.md", StringComparison.OrdinalIgnoreCase))?.ContentHash;
        var docsHash = CombineHashes(files.Where(f => f.RelativePath.StartsWith(".ai-context/docs/backend/", StringComparison.OrdinalIgnoreCase)).Select(f => f.ContentHash));
        var contextHash = CombineHashes(files.Where(f => f.RelativePath.StartsWith(".ai-context/", StringComparison.OrdinalIgnoreCase)).Select(f => f.ContentHash));

        var snapshotId = existing?.Id ?? files.First().SnapshotId;
        files = files.Select(f => f with { SnapshotId = snapshotId }).ToArray();
        var snapshot = new SourceSnapshot(
            snapshotId,
            projectId,
            active.Id,
            scanRunId,
            $"Scan {scan.CompletedAt?.ToLocalTime():yyyy-MM-dd HH:mm:ss}",
            scan.CompletedAt ?? scan.StartedAt,
            contextHash,
            handoffHash,
            docsHash,
            explanationHash,
            indexed.Count(f => !f.WasIgnored),
            indexed.Count(f => !f.WasIgnored && f.Extension.Equals(".cs", StringComparison.OrdinalIgnoreCase)),
            symbolCount,
            routeCount,
            flowCount,
            testCount,
            scan.TodosFound,
            generated.Count,
            gitMetadata?.BranchName,
            gitMetadata?.CommitSha,
            gitMetadata?.IsDirty,
            "Automatic snapshot created after a SourceWorker scan.");

        await snapshots.SaveAsync(snapshot, cancellationToken);
        await snapshotFiles.ReplaceForSnapshotAsync(projectId, snapshot.Id, files, cancellationToken);
        await metrics.ReplaceForSnapshotAsync(snapshot.Id, BuildMetrics(snapshot), cancellationToken);
        await branchRepository.SaveAsync(active with
        {
            LastSeenUtc = DateTimeOffset.UtcNow,
            LastScanRunId = scanRunId,
            LastSnapshotId = snapshot.Id,
            OptionalGitBranchName = gitMetadata?.BranchName,
            OptionalGitCommitSha = gitMetadata?.CommitSha,
            OptionalGitIsDirty = gitMetadata?.IsDirty
        }, cancellationToken);
        return snapshot;
    }

    public Task<IReadOnlyList<SourceSnapshot>> GetSnapshotsAsync(Guid projectId, Guid? branchId, CancellationToken cancellationToken)
        => branchId is Guid id ? snapshots.GetByBranchAsync(projectId, id, cancellationToken) : snapshots.GetByProjectAsync(projectId, cancellationToken);

    public Task<SourceSnapshot?> GetSnapshotAsync(Guid projectId, Guid snapshotId, CancellationToken cancellationToken)
        => snapshots.GetAsync(projectId, snapshotId, cancellationToken);

    public async Task<SourceSnapshot?> GetLatestSnapshotAsync(Guid projectId, CancellationToken cancellationToken)
        => (await snapshots.GetByProjectAsync(projectId, cancellationToken)).FirstOrDefault();

    public async Task<SourceSnapshot?> GetLatestSnapshotForBranchAsync(Guid projectId, Guid branchId, CancellationToken cancellationToken)
        => (await snapshots.GetByBranchAsync(projectId, branchId, cancellationToken)).FirstOrDefault();

    public async Task<SourceSnapshot?> GetPreviousSnapshotAsync(Guid projectId, Guid snapshotId, CancellationToken cancellationToken)
    {
        var current = await snapshots.GetAsync(projectId, snapshotId, cancellationToken);
        if (current is null) return null;
        return (await snapshots.GetByBranchAsync(projectId, current.ContextBranchId, cancellationToken))
            .Where(s => s.CreatedUtc < current.CreatedUtc || (s.CreatedUtc == current.CreatedUtc && s.Id != current.Id))
            .OrderByDescending(s => s.CreatedUtc)
            .FirstOrDefault();
    }

    public async Task DeleteSnapshotAsync(Guid projectId, Guid snapshotId, CancellationToken cancellationToken)
    {
        await snapshotFiles.ReplaceForSnapshotAsync(projectId, snapshotId, [], cancellationToken);
        await metrics.ReplaceForSnapshotAsync(snapshotId, [], cancellationToken);
        await snapshots.DeleteAsync(projectId, snapshotId, cancellationToken);
    }

    private static async Task<IReadOnlyList<SourceSnapshotFile>> BuildFilesAsync(Project project, Guid snapshotId, IReadOnlyList<IndexedFile> indexed, IReadOnlyList<GeneratedArtifact> generated, CancellationToken cancellationToken)
    {
        var byPath = new Dictionary<string, SourceSnapshotFile>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in indexed.Where(f => !f.WasIgnored))
        {
            byPath[file.RelativePath] = new SourceSnapshotFile(Guid.NewGuid(), snapshotId, project.Id, Normalize(file.RelativePath), KindFor(file), file.Sha256, file.SizeBytes, file.LastModifiedUtc, false, IsContextPath(file.RelativePath), file.Kind == IndexedFileKind.Source, file.Extension.Equals(".md", StringComparison.OrdinalIgnoreCase) || file.Extension.Equals(".markdown", StringComparison.OrdinalIgnoreCase), false, ImportanceFor(file.RelativePath, false));
        }

        foreach (var artifact in generated)
        {
            var relative = Normalize(artifact.RelativePath);
            var info = File.Exists(artifact.AbsolutePath) ? new FileInfo(artifact.AbsolutePath) : null;
            var content = await TryReadGeneratedMarkdownAsync(project.RootPath, relative, artifact.AbsolutePath, cancellationToken);
            byPath[relative] = new SourceSnapshotFile(
                Guid.NewGuid(),
                snapshotId,
                project.Id,
                relative,
                KindFor(relative, true),
                info is null ? artifact.ContentHash : await HashFileAsync(info.FullName, cancellationToken),
                info?.Length ?? 0,
                info?.LastWriteTimeUtc,
                true,
                IsContextPath(relative),
                false,
                relative.EndsWith(".md", StringComparison.OrdinalIgnoreCase) || relative.EndsWith(".markdown", StringComparison.OrdinalIgnoreCase),
                info is null,
                ImportanceFor(relative, true),
                content);
        }

        return byPath.Values.OrderByDescending(f => f.ImportanceScore).ThenBy(f => f.RelativePath).ToArray();
    }

    private static SourceSnapshotFileKind KindFor(IndexedFile file)
        => file.Kind switch
        {
            IndexedFileKind.Solution => SourceSnapshotFileKind.SolutionFile,
            IndexedFileKind.Project => SourceSnapshotFileKind.ProjectFile,
            IndexedFileKind.Config => SourceSnapshotFileKind.Config,
            IndexedFileKind.Markdown => SourceSnapshotFileKind.Markdown,
            IndexedFileKind.Source when file.RelativePath.Contains("test", StringComparison.OrdinalIgnoreCase) => SourceSnapshotFileKind.Test,
            IndexedFileKind.Source => SourceSnapshotFileKind.Source,
            _ => SourceSnapshotFileKind.Unknown
        };

    private static SourceSnapshotFileKind KindFor(string relativePath, bool generated)
    {
        if (relativePath.Equals(".ai-context/agent-handoff.md", StringComparison.OrdinalIgnoreCase)) return SourceSnapshotFileKind.AgentHandoff;
        if (relativePath.Equals(".ai-context/docs/backend/07-ai-explanation.md", StringComparison.OrdinalIgnoreCase)) return SourceSnapshotFileKind.AiExplanation;
        if (relativePath.StartsWith(".ai-context/docs/backend/", StringComparison.OrdinalIgnoreCase)) return SourceSnapshotFileKind.BackendDoc;
        if (relativePath.StartsWith(".ai-context/packs/", StringComparison.OrdinalIgnoreCase)) return SourceSnapshotFileKind.ContextPack;
        if (generated || relativePath.StartsWith(".ai-context/", StringComparison.OrdinalIgnoreCase)) return SourceSnapshotFileKind.GeneratedContext;
        return relativePath.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ? SourceSnapshotFileKind.Markdown : SourceSnapshotFileKind.Unknown;
    }

    private static IReadOnlyList<SourceSnapshotMetric> BuildMetrics(SourceSnapshot snapshot)
        =>
        [
            Metric(snapshot.Id, "indexedFiles", "scan", snapshot.IndexedFileCount),
            Metric(snapshot.Id, "csharpFiles", "scan", snapshot.CSharpFileCount),
            Metric(snapshot.Id, "symbols", "code-intelligence", snapshot.SymbolCount),
            Metric(snapshot.Id, "routes", "code-intelligence", snapshot.RouteCount),
            Metric(snapshot.Id, "flows", "code-intelligence", snapshot.FlowCount),
            Metric(snapshot.Id, "tests", "code-intelligence", snapshot.TestCount),
            Metric(snapshot.Id, "todos", "scan", snapshot.TodoCount),
            Metric(snapshot.Id, "generatedArtifacts", "context", snapshot.GeneratedArtifactCount)
        ];

    private static SourceSnapshotMetric Metric(Guid snapshotId, string name, string category, int value) => new(Guid.NewGuid(), snapshotId, name, category, value, null, null);

    private static async Task<string?> TryReadGeneratedMarkdownAsync(string root, string relativePath, string absolutePath, CancellationToken cancellationToken)
    {
        if (!IsGeneratedMarkdownDiffSafe(relativePath)) return null;
        var fullPath = Path.GetFullPath(string.IsNullOrWhiteSpace(absolutePath) ? Path.Combine(root, relativePath) : absolutePath);
        var projectRoot = Path.GetFullPath(root);
        if (!fullPath.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase) || !File.Exists(fullPath)) return null;
        return await File.ReadAllTextAsync(fullPath, cancellationToken);
    }

    private static bool IsGeneratedMarkdownDiffSafe(string relativePath)
        => (relativePath.EndsWith(".md", StringComparison.OrdinalIgnoreCase) || relativePath.EndsWith(".markdown", StringComparison.OrdinalIgnoreCase))
           && (relativePath.StartsWith(".ai-context/", StringComparison.OrdinalIgnoreCase) || relativePath.StartsWith("instructions/Result/Backend/", StringComparison.OrdinalIgnoreCase));

    private static bool IsContextPath(string relativePath) => relativePath.StartsWith(".ai-context/", StringComparison.OrdinalIgnoreCase) || relativePath.StartsWith("instructions/Result/", StringComparison.OrdinalIgnoreCase);
    private static int ImportanceFor(string relativePath, bool generated) => relativePath.Contains("agent-handoff", StringComparison.OrdinalIgnoreCase) ? 100 : relativePath.Contains("07-ai-explanation", StringComparison.OrdinalIgnoreCase) ? 95 : generated ? 60 : relativePath.EndsWith(".sln", StringComparison.OrdinalIgnoreCase) ? 40 : 10;
    private static string Normalize(string relativePath) => relativePath.Replace('\\', '/').TrimStart('/');

    private static async Task<string> HashFileAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string? CombineHashes(IEnumerable<string?> hashes)
    {
        var material = string.Join("|", hashes.Where(h => !string.IsNullOrWhiteSpace(h)).OrderBy(h => h));
        return string.IsNullOrEmpty(material) ? null : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(material))).ToLowerInvariant();
    }
}

public sealed class SourceDiffService(ISourceSnapshotRepository snapshots, ISourceSnapshotFileRepository files, ITextDiffService textDiff) : ISourceDiffService
{
    public async Task<SourceDiffReport> CompareSnapshotsAsync(Guid projectId, Guid leftSnapshotId, Guid rightSnapshotId, CancellationToken cancellationToken)
    {
        var left = await snapshots.GetAsync(projectId, leftSnapshotId, cancellationToken) ?? throw new InvalidOperationException("Left snapshot was not found.");
        var right = await snapshots.GetAsync(projectId, rightSnapshotId, cancellationToken) ?? throw new InvalidOperationException("Right snapshot was not found.");
        var leftFiles = (await files.GetBySnapshotAsync(projectId, left.Id, cancellationToken)).ToDictionary(f => f.RelativePath, StringComparer.OrdinalIgnoreCase);
        var rightFiles = (await files.GetBySnapshotAsync(projectId, right.Id, cancellationToken)).ToDictionary(f => f.RelativePath, StringComparer.OrdinalIgnoreCase);
        var changes = BuildFileChanges(leftFiles, rightFiles);
        var metricChanges = BuildMetricChanges(left, right);
        var codeChanges = metricChanges
            .Where(m => m.Category == "code-intelligence" || m.Category == "scan")
            .Where(m => m.Delta != "0")
            .Select(m => new SourceDiffCodeIntelligenceChange(m.Name, m.Name, null, SourceDiffChangeKind.Modified, $"{m.Name}: {m.LeftValue} -> {m.RightValue}"))
            .ToArray();
        var addedCount = changes.Count(c => c.ChangeKind == SourceDiffChangeKind.Added);
        var removedCount = changes.Count(c => c.ChangeKind == SourceDiffChangeKind.Removed);
        var modifiedCount = changes.Count(c => c.ChangeKind == SourceDiffChangeKind.Modified);
        var handoffChanged = changes.Any(c => c.RelativePath.Equals(".ai-context/agent-handoff.md", StringComparison.OrdinalIgnoreCase));
        var aiExplanationChanged = changes.Any(c => c.RelativePath.Equals(".ai-context/docs/backend/07-ai-explanation.md", StringComparison.OrdinalIgnoreCase));
        var docsChanged = changes.Any(c => c.FileKind is SourceSnapshotFileKind.BackendDoc or SourceSnapshotFileKind.AiExplanation or SourceSnapshotFileKind.GeneratedContext);
        var summary = new SourceDiffSummary(
            changes.Count,
            addedCount,
            removedCount,
            modifiedCount,
            docsChanged,
            handoffChanged,
            aiExplanationChanged,
            metricChanges.Any(m => m.Name == "symbols" && m.Delta != "0"),
            metricChanges.Any(m => m.Name == "flows" && m.Delta != "0"),
            metricChanges.Any(m => m.Name == "routes" && m.Delta != "0"),
            metricChanges.Any(m => m.Name == "tests" && m.Delta != "0"),
            metricChanges.Any(m => m.Name == "todos" && m.Delta != "0"),
            $"{changes.Count} files changed ({addedCount} added, {removedCount} removed, {modifiedCount} modified).");

        return new SourceDiffReport(projectId, left.Id, right.Id, left.ContextBranchId, right.ContextBranchId, summary, changes, metricChanges, changes.Where(c => c.FileKind == SourceSnapshotFileKind.AgentHandoff).ToArray(), changes.Where(c => c.FileKind is SourceSnapshotFileKind.BackendDoc or SourceSnapshotFileKind.AiExplanation).ToArray(), codeChanges.Where(c => c.Kind == "symbols").ToArray(), codeChanges.Where(c => c.Kind == "flows").ToArray(), codeChanges.Where(c => c.Kind == "routes").ToArray(), codeChanges.Where(c => c.Kind == "todos").ToArray(), codeChanges.Where(c => c.Kind == "tests").ToArray(), []);
    }

    public async Task<SourceDiffReport> CompareLatestWithPreviousAsync(Guid projectId, Guid branchId, CancellationToken cancellationToken)
    {
        var branchSnapshots = (await snapshots.GetByBranchAsync(projectId, branchId, cancellationToken)).OrderByDescending(s => s.CreatedUtc).ToArray();
        if (branchSnapshots.Length < 2)
        {
            var id = branchSnapshots.FirstOrDefault()?.Id ?? Guid.Empty;
            return new SourceDiffReport(projectId, id, id, branchId, branchId, new SourceDiffSummary(0, 0, 0, 0, false, false, false, false, false, false, false, false, "At least two snapshots are needed before SourceWorker can produce a branch diff."), [], [], [], [], [], [], [], [], [], ["At least two snapshots are needed before SourceWorker can produce a branch diff."]);
        }

        return await CompareSnapshotsAsync(projectId, branchSnapshots[1].Id, branchSnapshots[0].Id, cancellationToken);
    }

    public async Task<SourceDiffReport> CompareBranchLatestSnapshotsAsync(Guid projectId, Guid leftBranchId, Guid rightBranchId, CancellationToken cancellationToken)
    {
        var left = (await snapshots.GetByBranchAsync(projectId, leftBranchId, cancellationToken)).FirstOrDefault();
        var right = (await snapshots.GetByBranchAsync(projectId, rightBranchId, cancellationToken)).FirstOrDefault();
        if (left is null || right is null) throw new InvalidOperationException("Both branches need at least one snapshot.");
        return await CompareSnapshotsAsync(projectId, left.Id, right.Id, cancellationToken);
    }

    public async Task<SourceTextDiff> CompareFilesAsync(Guid projectId, Guid leftSnapshotId, Guid rightSnapshotId, string relativePath, CancellationToken cancellationToken)
    {
        if (relativePath.Contains("..", StringComparison.Ordinal) || Path.IsPathRooted(relativePath)) throw new InvalidOperationException("Unsafe relative path.");
        var left = (await files.GetBySnapshotAsync(projectId, leftSnapshotId, cancellationToken)).FirstOrDefault(f => f.RelativePath.Equals(relativePath, StringComparison.OrdinalIgnoreCase));
        var right = (await files.GetBySnapshotAsync(projectId, rightSnapshotId, cancellationToken)).FirstOrDefault(f => f.RelativePath.Equals(relativePath, StringComparison.OrdinalIgnoreCase));
        return textDiff.CreateSideBySideDiff(left?.TextContent, right?.TextContent, relativePath);
    }

    private IReadOnlyList<SourceDiffFileChange> BuildFileChanges(IReadOnlyDictionary<string, SourceSnapshotFile> left, IReadOnlyDictionary<string, SourceSnapshotFile> right)
    {
        var paths = left.Keys.Concat(right.Keys).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(p => p).ToArray();
        var result = new List<SourceDiffFileChange>();
        foreach (var path in paths)
        {
            left.TryGetValue(path, out var l);
            right.TryGetValue(path, out var r);
            var change = l is null ? SourceDiffChangeKind.Added : r is null ? SourceDiffChangeKind.Removed : !string.Equals(l.ContentHash, r.ContentHash, StringComparison.OrdinalIgnoreCase) ? SourceDiffChangeKind.Modified : SourceDiffChangeKind.Unchanged;
            if (change == SourceDiffChangeKind.Unchanged) continue;
            var diff = l?.TextContent is not null || r?.TextContent is not null ? textDiff.CreateSideBySideDiff(l?.TextContent, r?.TextContent, path) : null;
            var addedLines = diff?.Hunks.Sum(h => h.Lines.Count(line => line.Kind == SourceTextDiffLineKind.Added));
            var removedLines = diff?.Hunks.Sum(h => h.Lines.Count(line => line.Kind == SourceTextDiffLineKind.Removed));
            var modifiedLines = diff?.Hunks.Sum(h => h.Lines.Count(line => line.Kind == SourceTextDiffLineKind.Changed));
            result.Add(new SourceDiffFileChange(path, change, r?.FileKind ?? l?.FileKind ?? SourceSnapshotFileKind.Unknown, l?.ContentHash, r?.ContentHash, addedLines, removedLines, modifiedLines, diff is null ? null : textDiff.CreateUnifiedDiff(l?.TextContent, r?.TextContent, path), r?.IsGenerated ?? l?.IsGenerated ?? false, (r?.ImportanceScore ?? l?.ImportanceScore ?? 0) >= 60, diff is null ? "SourceWorker stores source history as hashes in this MVP; generated markdown includes readable text diffs." : null));
        }
        return result;
    }

    private static IReadOnlyList<SourceDiffMetricChange> BuildMetricChanges(SourceSnapshot left, SourceSnapshot right)
        =>
        [
            Metric("indexedFiles", "scan", left.IndexedFileCount, right.IndexedFileCount),
            Metric("csharpFiles", "scan", left.CSharpFileCount, right.CSharpFileCount),
            Metric("symbols", "code-intelligence", left.SymbolCount, right.SymbolCount),
            Metric("routes", "code-intelligence", left.RouteCount, right.RouteCount),
            Metric("flows", "code-intelligence", left.FlowCount, right.FlowCount),
            Metric("tests", "code-intelligence", left.TestCount, right.TestCount),
            Metric("todos", "scan", left.TodoCount, right.TodoCount),
            Metric("generatedArtifacts", "context", left.GeneratedArtifactCount, right.GeneratedArtifactCount)
        ];

    private static SourceDiffMetricChange Metric(string name, string category, int left, int right) => new(name, category, left.ToString(), right.ToString(), (right - left).ToString(), null);
}

public sealed class TextDiffService : ITextDiffService
{
    public string CreateUnifiedDiff(string? leftText, string? rightText, string path)
    {
        var diff = CreateSideBySideDiff(leftText, rightText, path);
        var builder = new StringBuilder();
        builder.AppendLine($"--- {path}");
        builder.AppendLine($"+++ {path}");
        foreach (var hunk in diff.Hunks)
        {
            builder.AppendLine($"@@ -{hunk.OldStart},{hunk.OldLength} +{hunk.NewStart},{hunk.NewLength} @@");
            foreach (var line in hunk.Lines)
            {
                builder.Append(line.Kind == SourceTextDiffLineKind.Added ? '+' : line.Kind == SourceTextDiffLineKind.Removed ? '-' : ' ');
                builder.AppendLine(line.Text);
            }
        }
        return builder.ToString();
    }

    public SourceTextDiff CreateSideBySideDiff(string? leftText, string? rightText, string path)
    {
        var left = Split(leftText);
        var right = Split(rightText);
        var rows = new List<SourceTextDiffLine>();
        var max = Math.Max(left.Length, right.Length);
        var added = 0;
        var removed = 0;
        var modified = 0;
        for (var i = 0; i < max; i++)
        {
            var l = i < left.Length ? left[i] : null;
            var r = i < right.Length ? right[i] : null;
            if (l == r && l is not null)
            {
                rows.Add(new SourceTextDiffLine(SourceTextDiffLineKind.Context, i + 1, i + 1, l));
            }
            else
            {
                if (l is not null)
                {
                    rows.Add(new SourceTextDiffLine(SourceTextDiffLineKind.Removed, i + 1, null, l));
                    removed++;
                }
                if (r is not null)
                {
                    rows.Add(new SourceTextDiffLine(SourceTextDiffLineKind.Added, null, i + 1, r));
                    added++;
                }
                if (l is not null && r is not null) modified++;
            }
        }

        return new SourceTextDiff(path, "left", "right", [new SourceTextDiffHunk(1, left.Length, 1, right.Length, rows)]);
    }

    public string CreateMarkdownDiff(string? leftText, string? rightText, string path)
        => $"```diff\n{CreateUnifiedDiff(leftText, rightText, path)}```";

    private static string[] Split(string? text) => (text ?? "").Replace("\r\n", "\n").Split('\n');
}

public sealed class BranchDiffPackService(IProjectRepository projects, ISourceSnapshotRepository snapshots, ISourceDiffService diffs, IGeneratedArtifactRepository? artifacts = null) : IBranchDiffPackService
{
    private const string TenScanDiffPath = ".ai-context/branches/diffs/10-scan-diff.md";

    public async Task<BranchDiffPack> BuildBranchDiffPackAsync(Guid projectId, Guid leftSnapshotId, Guid rightSnapshotId, CancellationToken cancellationToken)
    {
        var project = await projects.GetAsync(projectId, cancellationToken) ?? throw new InvalidOperationException("Project was not found.");
        var report = await diffs.CompareSnapshotsAsync(projectId, leftSnapshotId, rightSnapshotId, cancellationToken);
        var markdown = BuildMarkdown(project, report);
        return new BranchDiffPack(projectId, leftSnapshotId, rightSnapshotId, markdown, DateTimeOffset.UtcNow, ".ai-context/branches/diffs/latest-branch-diff.md");
    }

    public async Task<BranchDiffPack> BuildLatestBranchDiffPackAsync(Guid projectId, Guid branchId, CancellationToken cancellationToken)
    {
        var branchSnapshots = (await snapshots.GetByBranchAsync(projectId, branchId, cancellationToken)).OrderByDescending(s => s.CreatedUtc).ToArray();
        if (branchSnapshots.Length < 2) throw new InvalidOperationException("At least two snapshots are needed to build a branch diff pack.");
        return await BuildBranchDiffPackAsync(projectId, branchSnapshots[1].Id, branchSnapshots[0].Id, cancellationToken);
    }

    public async Task<BranchDiffPack> SaveBranchDiffPackAsync(Guid projectId, Guid leftSnapshotId, Guid rightSnapshotId, CancellationToken cancellationToken)
    {
        var pack = await BuildBranchDiffPackAsync(projectId, leftSnapshotId, rightSnapshotId, cancellationToken);
        var project = await projects.GetAsync(projectId, cancellationToken) ?? throw new InvalidOperationException("Project was not found.");
        var rightSnapshot = await snapshots.GetAsync(projectId, rightSnapshotId, cancellationToken);
        var relative = $".ai-context/branches/diffs/{leftSnapshotId:N}-{rightSnapshotId:N}.md";
        await WriteGeneratedDiffAsync(project, relative, pack.Markdown, rightSnapshot?.ScanRunId, cancellationToken);
        return pack with { SuggestedPath = relative };
    }

    public async Task<BranchDiffPack?> SaveLatestTenScanDiffAsync(Guid projectId, Guid branchId, CancellationToken cancellationToken)
        => await SaveScanDiffAsync(projectId, branchId, SourceBranchScanDiffScope.LatestScan, cancellationToken);

    public async Task<BranchDiffPack?> BuildScanDiffAsync(Guid projectId, Guid branchId, SourceBranchScanDiffScope scope, CancellationToken cancellationToken)
    {
        var project = await projects.GetAsync(projectId, cancellationToken) ?? throw new InvalidOperationException("Project was not found.");
        var branchSnapshots = (await snapshots.GetByBranchAsync(projectId, branchId, cancellationToken))
            .OrderByDescending(s => s.CreatedUtc)
            .ThenByDescending(s => s.Id)
            .ToArray();
        var selectedSnapshots = scope == SourceBranchScanDiffScope.AllBranchScans
            ? branchSnapshots
            : branchSnapshots.Take(2).ToArray();

        if (selectedSnapshots.Length < 2)
        {
            return new BranchDiffPack(projectId, Guid.Empty, selectedSnapshots.FirstOrDefault()?.Id ?? Guid.Empty, BuildScanDiffMarkdown(project, scope, selectedSnapshots, []), DateTimeOffset.UtcNow, TenScanDiffPath);
        }

        var reports = new List<SourceDiffReport>();
        for (var i = 0; i < selectedSnapshots.Length - 1; i++)
        {
            reports.Add(await diffs.CompareSnapshotsAsync(projectId, selectedSnapshots[i + 1].Id, selectedSnapshots[i].Id, cancellationToken));
        }

        return new BranchDiffPack(projectId, reports[0].LeftSnapshotId, reports[0].RightSnapshotId, BuildScanDiffMarkdown(project, scope, selectedSnapshots, reports), DateTimeOffset.UtcNow, TenScanDiffPath);
    }

    public async Task<BranchDiffPack?> SaveScanDiffAsync(Guid projectId, Guid branchId, SourceBranchScanDiffScope scope, CancellationToken cancellationToken)
    {
        var pack = await BuildScanDiffAsync(projectId, branchId, scope, cancellationToken);
        if (pack is null) return null;

        var project = await projects.GetAsync(projectId, cancellationToken) ?? throw new InvalidOperationException("Project was not found.");
        var rightSnapshot = pack.RightSnapshotId == Guid.Empty ? null : await snapshots.GetAsync(projectId, pack.RightSnapshotId, cancellationToken);
        await WriteGeneratedDiffAsync(project, TenScanDiffPath, pack.Markdown, rightSnapshot?.ScanRunId, cancellationToken);
        return pack;
    }

    private static string BuildMarkdown(Project project, SourceDiffReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# SourceWorker Branch Diff");
        builder.AppendLine();
        builder.AppendLine($"Project: {project.Name}");
        builder.AppendLine($"Generated UTC: {DateTimeOffset.UtcNow:O}");
        builder.AppendLine();
        builder.AppendLine("## Summary");
        builder.AppendLine($"- Added files: {report.Summary.AddedFileCount}");
        builder.AppendLine($"- Removed files: {report.Summary.RemovedFileCount}");
        builder.AppendLine($"- Modified files: {report.Summary.ModifiedFileCount}");
        builder.AppendLine($"- Generated docs changed: {report.Summary.GeneratedDocsChanged}");
        builder.AppendLine();
        builder.AppendLine("## Changed Files");
        foreach (var change in report.FileChanges.Take(100))
        {
            builder.AppendLine($"- {change.ChangeKind}: {change.RelativePath}");
        }
        builder.AppendLine();
        builder.AppendLine("## Recommended Agent Prompt");
        builder.AppendLine("Use this diff as SourceWorker context. Inspect real source files before editing. Do not infer method names from this diff alone.");
        return builder.ToString();
    }

    private static string BuildScanDiffMarkdown(Project project, SourceBranchScanDiffScope scope, IReadOnlyList<SourceSnapshot> snapshots, IReadOnlyList<SourceDiffReport> reports)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# SourceWorker Scan Diff");
        builder.AppendLine();
        builder.AppendLine($"Project: {project.Name}");
        builder.AppendLine($"Scope: {scope}");
        builder.AppendLine($"Generated UTC: {DateTimeOffset.UtcNow:O}");
        builder.AppendLine($"Snapshots considered: {snapshots.Count}");
        builder.AppendLine();
        if (reports.Count == 0)
        {
            builder.AppendLine("At least two snapshots are needed before SourceWorker can list changed scan diffs.");
            return builder.ToString();
        }

        builder.AppendLine("## Summary");
        builder.AppendLine($"- Scan comparisons: {reports.Count}");
        builder.AppendLine($"- Changed files across comparisons: {reports.Sum(r => r.Summary.ChangedFileCount)}");
        builder.AppendLine($"- Added file entries: {reports.Sum(r => r.Summary.AddedFileCount)}");
        builder.AppendLine($"- Removed file entries: {reports.Sum(r => r.Summary.RemovedFileCount)}");
        builder.AppendLine($"- Modified file entries: {reports.Sum(r => r.Summary.ModifiedFileCount)}");
        builder.AppendLine();

        foreach (var report in reports)
        {
            var right = snapshots.FirstOrDefault(s => s.Id == report.RightSnapshotId);
            var left = snapshots.FirstOrDefault(s => s.Id == report.LeftSnapshotId);
            builder.AppendLine($"## {Label(left)} -> {Label(right)}");
            builder.AppendLine();
            builder.AppendLine(report.Summary.HumanSummary);
            builder.AppendLine();

            if (report.MetricChanges.Any(m => m.Delta != "0"))
            {
                builder.AppendLine("### Metrics");
                foreach (var metric in report.MetricChanges.Where(m => m.Delta != "0"))
                {
                    builder.AppendLine($"- {metric.Name}: {metric.LeftValue} -> {metric.RightValue} ({metric.Delta})");
                }
                builder.AppendLine();
            }

            builder.AppendLine("### Changed Files");
            foreach (var change in report.FileChanges.Take(100))
            {
                var lineCounts = change.AddedLines is null && change.RemovedLines is null
                    ? ""
                    : $" (+{change.AddedLines ?? 0}/-{change.RemovedLines ?? 0})";
                builder.AppendLine($"- {change.ChangeKind}: `{change.RelativePath}`{lineCounts}");
            }
            builder.AppendLine();
        }

        return builder.ToString();
    }

    private async Task WriteGeneratedDiffAsync(Project project, string relativePath, string markdown, Guid? scanRunId, CancellationToken cancellationToken)
    {
        var full = Path.Combine(project.RootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        await File.WriteAllTextAsync(full, markdown, cancellationToken);

        if (artifacts is not null)
        {
            var hash = Hash(markdown);
            await artifacts.UpsertAsync(new GeneratedArtifact(Guid.NewGuid(), project.Id, scanRunId, "source-context-branch-diff", relativePath, full, GeneratedArtifactType.InternalContextFile, hash, DateTimeOffset.UtcNow, hash, GeneratedArtifactStatus.Generated, "SourceWorker.BranchDiff", null), cancellationToken);
        }
    }

    private static string Label(SourceSnapshot? snapshot)
        => snapshot is null ? "unknown snapshot" : $"{snapshot.Name} ({snapshot.CreatedUtc:yyyy-MM-dd HH:mm:ss} UTC)";

    private static string Hash(string content)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant();
}

public sealed class OptionalGitMetadataService : IOptionalGitMetadataService
{
    public async Task<OptionalGitMetadata?> TryGetGitMetadataAsync(string projectRoot, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(Path.Combine(projectRoot, ".git"))) return null;
        try
        {
            var branch = await Git(projectRoot, "rev-parse --abbrev-ref HEAD", cancellationToken);
            var sha = await Git(projectRoot, "rev-parse HEAD", cancellationToken);
            var status = await Git(projectRoot, "status --porcelain", cancellationToken);
            return new OptionalGitMetadata(branch.Trim(), sha.Trim(), !string.IsNullOrWhiteSpace(status));
        }
        catch
        {
            return null;
        }
    }

    private static async Task<string> Git(string root, string arguments, CancellationToken cancellationToken)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo("git", arguments)
        {
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            UseShellExecute = false
        };
        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        if (process.ExitCode != 0) throw new InvalidOperationException("Git metadata probe failed.");
        return output;
    }
}
