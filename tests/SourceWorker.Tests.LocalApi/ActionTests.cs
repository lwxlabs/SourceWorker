using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using SourceWorker.Contracts;
using System.Net;
using System.Net.Http.Json;

namespace SourceWorker.Tests.LocalApi;

public sealed class ActionTests
{
    [Fact]
    public async Task Project_registration_requires_auth_when_enabled()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/projects", new { path = "C:\\test" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Project_registration_works_with_valid_request()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), "sw-reg-" + Guid.NewGuid());
        Directory.CreateDirectory(tempPath);
        
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?> { ["SourceWorker:LocalApi:RequireToken"] = "false" })));
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/projects", new { path = tempPath, name = "Test Project" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var result = await response.Content.ReadFromJsonAsync<CreateProjectResponse>();
        Assert.NotNull(result);
        Assert.Equal("Test Project", result!.Project.Name);
        Assert.Equal(tempPath, result.Project.RootPath);
    }

    [Fact]
    public async Task Job_list_endpoints_work()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?> { ["SourceWorker:LocalApi:RequireToken"] = "false" })));
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/jobs");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var jobs = await response.Content.ReadFromJsonAsync<JobStatusDto[]>();
        Assert.NotNull(jobs);
    }

    [Fact]
    public async Task Cleanup_preview_requires_auth_when_enabled()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/projects/{Guid.NewGuid()}/cleanup-preview");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Cleanup_generated_requires_auth_when_enabled()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync($"/api/projects/{Guid.NewGuid()}/cleanup-generated", new { dryRun = true });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Export_docs_requires_auth_when_enabled()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var response = await client.PostAsync($"/api/projects/{Guid.NewGuid()}/exports/test-profile/run", null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Pack_export_default_requires_auth_when_enabled()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var response = await client.PostAsync($"/api/projects/{Guid.NewGuid()}/packs/test-pack/export-default", null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
