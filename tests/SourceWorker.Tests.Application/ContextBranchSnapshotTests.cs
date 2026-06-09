using LwX.Flow;
using Microsoft.Extensions.DependencyInjection;
using SourceWorker.Application;
using SourceWorker.Domain;
using SourceWorker.Infrastructure;
using SourceWorker.Worker;

namespace SourceWorker.Tests.Application;

public sealed class ContextBranchSnapshotTests
{
    [Fact]
    public async Task ContextBranch_creates_default_branch_and_keeps_one_active_branch()
    {
        using var workspace = new TemporaryProject();
        var services = CreateServices();
        var flow = services.GetRequiredService<IRequestFlow>();

        var project = await flow.Send(new AddProjectCommand(workspace.Root, "Branch Test"));
        var defaultBranches = await flow.Send(new GetContextBranchesQuery(project.Id));
        var userBranch = await flow.Send(new CreateContextBranchCommand(project.Id, "before-refactor", "Before refactor"));

        await flow.Send(new ActivateContextBranchCommand(project.Id, userBranch.Id));
        var branches = await flow.Send(new GetContextBranchesQuery(project.Id));

        Assert.Contains(branches, branch => branch.Name == "main-context" && branch.IsDefault);
        Assert.Contains(branches, branch => branch.Name == "before-refactor" && branch.IsActive);
        Assert.Single(branches, branch => branch.IsActive);
    }

    [Fact]
    public async Task SnapshotDiff_detects_generated_markdown_change_but_keeps_source_content_hash_only()
    {
        using var workspace = new TemporaryProject();
        var services = CreateServices();
        var flow = services.GetRequiredService<IRequestFlow>();
        var snapshots = services.GetRequiredService<ISourceSnapshotRepository>();
        var files = services.GetRequiredService<ISourceSnapshotFileRepository>();

        var project = await flow.Send(new AddProjectCommand(workspace.Root, "Snapshot Test"));
        var branch = (await flow.Send(new GetContextBranchesQuery(project.Id))).Single(branch => branch.IsActive);
        var left = Snapshot(project.Id, branch.Id, "left", symbols: 10, tests: 2);
        var right = Snapshot(project.Id, branch.Id, "right", symbols: 12, tests: 3);
        await snapshots.SaveAsync(left, CancellationToken.None);
        await snapshots.SaveAsync(right, CancellationToken.None);
        await files.ReplaceForSnapshotAsync(project.Id, left.Id,
        [
            GeneratedMarkdown(left, project.Id, ".ai-context/agent-handoff.md", "# Handoff\nOld line"),
            SourceHash(left, project.Id, "src/App.cs", "hash-left")
        ], CancellationToken.None);
        await files.ReplaceForSnapshotAsync(project.Id, right.Id,
        [
            GeneratedMarkdown(right, project.Id, ".ai-context/agent-handoff.md", "# Handoff\nNew line"),
            SourceHash(right, project.Id, "src/App.cs", "hash-right")
        ], CancellationToken.None);

        var diff = await flow.Send(new CompareSnapshotsQuery(project.Id, left.Id, right.Id));

        Assert.True(diff.Summary.HandoffChanged);
        Assert.True(diff.Summary.SymbolsChanged);
        Assert.True(diff.Summary.TestsChanged);
        var handoff = Assert.Single(diff.HandoffChanges);
        Assert.Contains("+New line", handoff.DiffPreview);
        var source = Assert.Single(diff.FileChanges, change => change.RelativePath == "src/App.cs");
        Assert.Null(source.DiffPreview);
        Assert.Contains("hashes", source.Explanation);
    }

    [Fact]
    public async Task GitMetadata_returns_null_for_non_git_project()
    {
        using var workspace = new TemporaryProject();
        var services = CreateServices();
        var git = services.GetRequiredService<IOptionalGitMetadataService>();

        var metadata = await git.TryGetGitMetadataAsync(workspace.Root, CancellationToken.None);

        Assert.Null(metadata);
    }

    [Fact]
    public async Task ProjectScanWorker_creates_snapshot_for_active_branch()
    {
        using var workspace = new TemporaryProject();
        File.WriteAllText(Path.Combine(workspace.Root, "README.md"), "# Worker Snapshot Test");
        File.WriteAllText(Path.Combine(workspace.Root, "WorkerSnapshot.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
        var services = CreateServices();
        var flow = services.GetRequiredService<IRequestFlow>();
        var jobs = services.GetRequiredService<IJobRepository>();

        var project = await flow.Send(new AddProjectCommand(workspace.Root, "Worker Snapshot"));
        var branch = await flow.Send(new CreateContextBranchCommand(project.Id, "worker-branch", "Worker branch"));
        await flow.Send(new ActivateContextBranchCommand(project.Id, branch.Id));
        var job = await jobs.AddAsync(new WorkerJob(Guid.NewGuid(), project.Id, WorkerJobType.ProjectScan, WorkerJobStatus.Queued, 0, "Queued", DateTimeOffset.UtcNow), CancellationToken.None);

        await ProjectScanWorker.ExecuteJobAsync(job.Id, services, CancellationToken.None);

        var branches = await flow.Send(new GetContextBranchesQuery(project.Id));
        var active = Assert.Single(branches, item => item.IsActive);
        var snapshots = await flow.Send(new GetSnapshotsQuery(project.Id, active.Id));
        Assert.Equal("worker-branch", active.Name);
        Assert.NotNull(active.LastSnapshotId);
        Assert.NotEmpty(snapshots);
        Assert.Equal(active.LastSnapshotId, snapshots.First().Id);
    }

    [Fact]
    public async Task ProjectScanWorker_writes_rolling_ten_scan_diff_after_each_scan()
    {
        using var workspace = new TemporaryProject();
        var readme = Path.Combine(workspace.Root, "README.md");
        File.WriteAllText(readme, "# Rolling Diff Test");
        File.WriteAllText(Path.Combine(workspace.Root, "RollingDiff.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
        var services = CreateServices();
        var flow = services.GetRequiredService<IRequestFlow>();
        var jobs = services.GetRequiredService<IJobRepository>();
        var artifacts = services.GetRequiredService<IGeneratedArtifactRepository>();

        var project = await flow.Send(new AddProjectCommand(workspace.Root, "Rolling Diff"));
        var firstJob = await jobs.AddAsync(new WorkerJob(Guid.NewGuid(), project.Id, WorkerJobType.ProjectScan, WorkerJobStatus.Queued, 0, "Queued", DateTimeOffset.UtcNow), CancellationToken.None);
        await ProjectScanWorker.ExecuteJobAsync(firstJob.Id, services, CancellationToken.None);

        File.AppendAllText(readme, $"{Environment.NewLine}Second scan changed this file.");
        var secondJob = await jobs.AddAsync(new WorkerJob(Guid.NewGuid(), project.Id, WorkerJobType.ProjectScan, WorkerJobStatus.Queued, 0, "Queued", DateTimeOffset.UtcNow), CancellationToken.None);
        await ProjectScanWorker.ExecuteJobAsync(secondJob.Id, services, CancellationToken.None);

        var relativePath = ".ai-context/branches/diffs/10-scan-diff.md";
        var absolutePath = Path.Combine(workspace.Root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        var markdown = await File.ReadAllTextAsync(absolutePath);
        var tracked = await artifacts.GetByPathAsync(project.Id, relativePath, CancellationToken.None);

        Assert.Contains("# SourceWorker Scan Diff", markdown);
        Assert.Contains("README.md", markdown);
        Assert.NotNull(tracked);
        Assert.Equal(GeneratedArtifactType.InternalContextFile, tracked!.Type);
    }

    private static ServiceProvider CreateServices()
        => new ServiceCollection()
            .AddSourceWorkerApplication()
            .AddTestSourceWorkerInfrastructure()
            .BuildServiceProvider();

    private static SourceSnapshot Snapshot(Guid projectId, Guid branchId, string name, int symbols, int tests)
        => new(
            Guid.NewGuid(),
            projectId,
            branchId,
            Guid.NewGuid(),
            name,
            DateTimeOffset.UtcNow,
            "context-" + name,
            "handoff-" + name,
            "docs-" + name,
            null,
            2,
            1,
            symbols,
            0,
            0,
            tests,
            0,
            1,
            null,
            null,
            null,
            null);

    private static SourceSnapshotFile GeneratedMarkdown(SourceSnapshot snapshot, Guid projectId, string relativePath, string text)
        => new(Guid.NewGuid(), snapshot.Id, projectId, relativePath, SourceSnapshotFileKind.AgentHandoff, Hash(text), text.Length, DateTimeOffset.UtcNow, true, true, false, true, false, 100, text);

    private static SourceSnapshotFile SourceHash(SourceSnapshot snapshot, Guid projectId, string relativePath, string hash)
        => new(Guid.NewGuid(), snapshot.Id, projectId, relativePath, SourceSnapshotFileKind.Source, hash, 10, DateTimeOffset.UtcNow, false, false, true, false, false, 10, null);

    private static string Hash(string text) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text))).ToLowerInvariant();

    private sealed class TemporaryProject : IDisposable
    {
        public TemporaryProject()
        {
            Root = Path.Combine(Path.GetTempPath(), "sourceworker-branches-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
        }

        public string Root { get; }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Root, recursive: true);
            }
            catch
            {
                // Best-effort cleanup for temp test folders.
            }
        }
    }
}
