using System.Net;
using System.Net.Http.Json;
using SourceWorker.Contracts;
using SourceWorker.Domain;

namespace SourceWorker.Tests.LocalApi;

public sealed class ContextBranchSnapshotDiffEndpointTests
{
    [Fact]
    public async Task ContextBranch_snapshot_and_diff_endpoints_require_auth()
    {
        await using var factory = TestApiFactory.Create(requireToken: true);
        using var client = factory.CreateClient();
        var projectId = Guid.NewGuid();
        var branchId = Guid.NewGuid();

        var branches = await client.GetAsync($"/api/projects/{projectId}/context-branches");
        var snapshots = await client.GetAsync($"/api/projects/{projectId}/context-branches/{branchId}/snapshots");
        var diff = await client.GetAsync($"/api/projects/{projectId}/snapshot-diff/latest?branchId={branchId}");
        var branchDiff = await client.GetAsync($"/api/projects/{projectId}/snapshot-diff/branches?leftBranchId={branchId}&rightBranchId={Guid.NewGuid()}");
        var pack = await client.PostAsJsonAsync($"/api/projects/{projectId}/snapshot-diff/pack", new { leftSnapshotId = Guid.NewGuid(), rightSnapshotId = Guid.NewGuid() });

        Assert.Equal(HttpStatusCode.Unauthorized, branches.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, snapshots.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, diff.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, branchDiff.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, pack.StatusCode);
    }

    [Fact]
    public async Task ContextBranch_create_activate_and_snapshot_routes_work_without_git()
    {
        using var temp = new TemporaryProject();
        await using var factory = TestApiFactory.Create(requireToken: false);
        using var client = factory.CreateClient();
        var created = await (await client.PostAsJsonAsync("/api/projects", new AddProjectRequest(temp.Root, "Branch API"))).Content.ReadFromJsonAsync<CreateProjectResponse>();

        var defaultBranches = await client.GetFromJsonAsync<SourceContextBranch[]>($"/api/projects/{created!.Project.Id}/context-branches");
        var createdBranch = await (await client.PostAsJsonAsync($"/api/projects/{created.Project.Id}/context-branches", new { name = "api-context", description = "API branch" })).Content.ReadFromJsonAsync<SourceContextBranch>();
        var activated = await (await client.PostAsync($"/api/projects/{created.Project.Id}/context-branches/{createdBranch!.Id}/activate", null)).Content.ReadFromJsonAsync<SourceContextBranch>();
        var snapshots = await client.GetAsync($"/api/projects/{created.Project.Id}/context-branches/{createdBranch.Id}/snapshots");

        Assert.NotNull(defaultBranches);
        Assert.Contains(defaultBranches!, branch => branch.Name == "main-context");
        Assert.Equal("api-context", createdBranch.Name);
        Assert.True(activated!.IsActive);
        Assert.Equal(HttpStatusCode.OK, snapshots.StatusCode);
    }

    [Fact]
    public async Task Diff_latest_returns_clear_warning_when_branch_has_fewer_than_two_snapshots()
    {
        using var temp = new TemporaryProject();
        await using var factory = TestApiFactory.Create(requireToken: false);
        using var client = factory.CreateClient();
        var created = await (await client.PostAsJsonAsync("/api/projects", new AddProjectRequest(temp.Root, "Diff API"))).Content.ReadFromJsonAsync<CreateProjectResponse>();
        var branches = await client.GetFromJsonAsync<SourceContextBranch[]>($"/api/projects/{created!.Project.Id}/context-branches");
        var branch = Assert.Single(branches!, item => item.IsActive);

        var response = await client.GetFromJsonAsync<SourceDiffReport>($"/api/projects/{created.Project.Id}/snapshot-diff/latest?branchId={branch.Id}");

        Assert.NotNull(response);
        Assert.Contains(response!.Warnings, warning => warning.Contains("At least two snapshots", StringComparison.OrdinalIgnoreCase));
    }

    private sealed class TemporaryProject : IDisposable
    {
        public TemporaryProject()
        {
            Root = Path.Combine(Path.GetTempPath(), "sourceworker-localapi-branches-" + Guid.NewGuid().ToString("N"));
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
