using SourceWorker.Contracts;
using System.Net.Http.Json;

namespace SourceWorker.Tests.LocalApi;

public sealed class ProjectFilteringTests
{
    [Fact]
    public async Task Projects_search_works()
    {
        var root = Path.Combine(Path.GetTempPath(), "sw-filter-" + Guid.NewGuid());
        Directory.CreateDirectory(root);

        await using var factory = TestApiFactory.Create(requireToken: false);
        using var client = factory.CreateClient();

        var alphaPath = Path.Combine(root, "AlphaProject");
        var betaPath = Path.Combine(root, "BetaProject");
        Directory.CreateDirectory(alphaPath);
        Directory.CreateDirectory(betaPath);

        await client.PostAsJsonAsync("/api/projects", new AddProjectRequest(alphaPath, "Alpha"));
        await client.PostAsJsonAsync("/api/projects", new AddProjectRequest(betaPath, "Beta"));

        var results = await client.GetFromJsonAsync<ProjectSummaryDto[]>("/api/projects?search=Alpha");

        Assert.NotNull(results);
        Assert.Contains(results!, p => p.Name == "Alpha");
        Assert.DoesNotContain(results!, p => p.Name == "Beta");
    }

    [Fact]
    public async Task Projects_includeMissing_false_hides_missing_paths()
    {
        await using var factory = TestApiFactory.Create(requireToken: false);
        using var client = factory.CreateClient();

        var missingPath = Path.Combine(Path.GetTempPath(), "sw-missing-" + Guid.NewGuid());
        await client.PostAsJsonAsync("/api/projects", new AddProjectRequest(missingPath, "MissingProject"));

        var results = await client.GetFromJsonAsync<ProjectSummaryDto[]>("/api/projects?includeMissing=false");

        Assert.NotNull(results);
        Assert.DoesNotContain(results!, p => p.Path == missingPath);
    }
}
