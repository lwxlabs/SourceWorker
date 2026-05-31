using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using System.Net.Http.Json;
using SourceWorker.Contracts;

namespace SourceWorker.Tests.LocalApi;

public sealed class HealthTests
{
    [Fact]
    public async Task Health_returns_ok()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?> { ["SourceWorker:LocalApi:RequireToken"] = "false" })));
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Engine_endpoints_return_ok()
    {
        var root = Path.Combine(Path.GetTempPath(), "sourceworker-api-" + Guid.NewGuid());
        Directory.CreateDirectory(Path.Combine(root, "src"));
        await File.WriteAllTextAsync(Path.Combine(root, "ApiSample.sln"), "");
        await File.WriteAllTextAsync(Path.Combine(root, "src", "ApiSample.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk.Web\"></Project>");

        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?> { ["SourceWorker:LocalApi:RequireToken"] = "false" })));
        using var client = factory.CreateClient();
        var created = await (await client.PostAsJsonAsync("/api/projects", new AddProjectRequest(root, "ApiSample"))).Content.ReadFromJsonAsync<CreateProjectResponse>();

        Assert.NotNull(created);
        (await client.GetAsync($"/api/projects/{created!.Project.Id}/doctor")).EnsureSuccessStatusCode();
        (await client.GetAsync($"/api/projects/{created.Project.Id}/changes")).EnsureSuccessStatusCode();
        (await client.GetAsync($"/api/projects/{created.Project.Id}/context-status")).EnsureSuccessStatusCode();
        (await client.GetAsync($"/api/projects/{created.Project.Id}/scan-runs")).EnsureSuccessStatusCode();
        await client.PostAsync($"/api/projects/{created.Project.Id}/scan", null);
        (await client.GetAsync("/api/storage/info")).EnsureSuccessStatusCode();
        (await client.GetAsync($"/api/projects/{created.Project.Id}/artifacts")).EnsureSuccessStatusCode();
        (await client.GetAsync($"/api/projects/{created.Project.Id}/cleanup-preview")).EnsureSuccessStatusCode();
        (await client.PostAsJsonAsync($"/api/projects/{created.Project.Id}/cleanup-generated", new CleanupGeneratedRequest(true, false))).EnsureSuccessStatusCode();
        (await client.GetAsync($"/api/projects/{created.Project.Id}/symbols")).EnsureSuccessStatusCode();
        (await client.GetAsync($"/api/projects/{created.Project.Id}/flows")).EnsureSuccessStatusCode();
        (await client.GetAsync($"/api/projects/{created.Project.Id}/routes")).EnsureSuccessStatusCode();
        (await client.GetAsync($"/api/projects/{created.Project.Id}/context-browser")).EnsureSuccessStatusCode();
        (await client.GetAsync($"/api/projects/{created.Project.Id}/context-browser/files")).EnsureSuccessStatusCode();
        (await client.GetAsync($"/api/projects/{created.Project.Id}/context-browser/search?query=Sample")).EnsureSuccessStatusCode();
        (await client.GetAsync($"/api/projects/{created.Project.Id}/outputs")).EnsureSuccessStatusCode();
        (await client.GetAsync($"/api/projects/{created.Project.Id}/packs")).EnsureSuccessStatusCode();
        (await client.GetAsync($"/api/projects/{created.Project.Id}/packs/backend-docs")).EnsureSuccessStatusCode();
        (await client.GetAsync($"/api/projects/{created.Project.Id}/packs/backend-docs/content")).EnsureSuccessStatusCode();
        (await client.PostAsync($"/api/projects/{created.Project.Id}/exports/backend-docs-compact/run", null)).EnsureSuccessStatusCode();
    }
}
