using SourceWorker.Contracts;
using System.Net;
using System.Net.Http.Json;

namespace SourceWorker.Tests.LocalApi;

public sealed class ActionTests
{
    [Fact]
    public async Task Project_registration_requires_auth_when_enabled()
    {
        await using var factory = TestApiFactory.Create();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/projects", new { path = "C:\\test" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Project_registration_works_with_valid_request()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), "sw-reg-" + Guid.NewGuid());
        Directory.CreateDirectory(tempPath);

        await using var factory = TestApiFactory.Create(requireToken: false);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/projects", new { path = tempPath, name = "Test Project" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<CreateProjectResponse>();
        Assert.NotNull(result);
        Assert.Equal("Test Project", result!.Project.Name);
        Assert.Equal(tempPath, result.Project.RootPath);
    }

    [Fact]
    public async Task Project_delete_unregisters_project()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), "sw-delete-" + Guid.NewGuid());
        Directory.CreateDirectory(tempPath);

        await using var factory = TestApiFactory.Create(requireToken: false);
        using var client = factory.CreateClient();

        var createdResponse = await client.PostAsJsonAsync("/api/projects", new { path = tempPath, name = "Delete Project" });
        createdResponse.EnsureSuccessStatusCode();
        var created = await createdResponse.Content.ReadFromJsonAsync<CreateProjectResponse>();

        var deleteResponse = await client.DeleteAsync($"/api/projects/{created!.Project.Id}");
        var projects = await client.GetFromJsonAsync<ProjectSummaryDto[]>("/api/projects?includeMissing=true");

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        Assert.DoesNotContain(projects!, p => p.Id == created.Project.Id);
    }

    [Fact]
    public async Task Project_delete_requires_auth_when_enabled()
    {
        await using var factory = TestApiFactory.Create();
        using var client = factory.CreateClient();

        var response = await client.DeleteAsync($"/api/projects/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Job_list_endpoints_work()
    {
        await using var factory = TestApiFactory.Create(requireToken: false);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/jobs");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var jobs = await response.Content.ReadFromJsonAsync<JobStatusDto[]>();
        Assert.NotNull(jobs);
    }

    [Fact]
    public async Task Cleanup_preview_requires_auth_when_enabled()
    {
        await using var factory = TestApiFactory.Create();
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/projects/{Guid.NewGuid()}/cleanup-preview");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Handoff_and_completeness_endpoints_require_auth_when_enabled()
    {
        await using var factory = TestApiFactory.Create();
        using var client = factory.CreateClient();
        var projectId = Guid.NewGuid();

        var handoff = await client.GetAsync($"/api/projects/{projectId}/agent-handoff");
        var completeness = await client.GetAsync($"/api/projects/{projectId}/scan-completeness");

        Assert.Equal(HttpStatusCode.Unauthorized, handoff.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, completeness.StatusCode);
    }

    [Fact]
    public async Task Cleanup_generated_requires_auth_when_enabled()
    {
        await using var factory = TestApiFactory.Create();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync($"/api/projects/{Guid.NewGuid()}/cleanup-generated", new { dryRun = true });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Export_docs_requires_auth_when_enabled()
    {
        await using var factory = TestApiFactory.Create();
        using var client = factory.CreateClient();

        var response = await client.PostAsync($"/api/projects/{Guid.NewGuid()}/exports/test-profile/run", null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Pack_export_default_requires_auth_when_enabled()
    {
        await using var factory = TestApiFactory.Create();
        using var client = factory.CreateClient();

        var response = await client.PostAsync($"/api/projects/{Guid.NewGuid()}/packs/test-pack/export-default", null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
