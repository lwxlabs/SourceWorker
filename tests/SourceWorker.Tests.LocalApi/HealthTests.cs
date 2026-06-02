using System.Net.Http.Json;
using SourceWorker.Contracts;
using SourceWorker.Domain;

namespace SourceWorker.Tests.LocalApi;

public sealed class HealthTests
{
    [Fact]
    public async Task Health_returns_ok()
    {
        await using var factory = TestApiFactory.Create(requireToken: false);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Storage_info_uses_isolated_test_database()
    {
        await using var factory = TestApiFactory.Create(requireToken: false);
        using var client = factory.CreateClient();

        var info = await client.GetFromJsonAsync<StorageInfo>("/api/storage/info");

        Assert.NotNull(info);
        Assert.DoesNotContain(Path.Combine("AppData", "Local", "SourceWorker", "sourceworker.localite.db"), info!.Schema.StoragePath, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(Path.Combine(Path.GetTempPath(), "sourceworker-localapi-test-db"), info.Schema.StoragePath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Engine_endpoints_return_ok()
    {
        var root = Path.Combine(Path.GetTempPath(), "sourceworker-api-" + Guid.NewGuid());
        Directory.CreateDirectory(Path.Combine(root, "src"));
        await File.WriteAllTextAsync(Path.Combine(root, "ApiSample.sln"), "");
        await File.WriteAllTextAsync(Path.Combine(root, "src", "ApiSample.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk.Web\"></Project>");

        await using var factory = TestApiFactory.Create(requireToken: false);
        using var client = factory.CreateClient();
        var created = await (await client.PostAsJsonAsync("/api/projects", new AddProjectRequest(root, "ApiSample"))).Content.ReadFromJsonAsync<CreateProjectResponse>();

        Assert.NotNull(created);
        (await client.GetAsync($"/api/projects/{created!.Project.Id}/doctor")).EnsureSuccessStatusCode();
        (await client.GetAsync($"/api/projects/{created.Project.Id}/changes")).EnsureSuccessStatusCode();
        (await client.GetAsync($"/api/projects/{created.Project.Id}/context-status")).EnsureSuccessStatusCode();
        (await client.GetAsync($"/api/projects/{created.Project.Id}/agent-handoff")).EnsureSuccessStatusCode();
        (await client.GetAsync($"/api/projects/{created.Project.Id}/scan-completeness")).EnsureSuccessStatusCode();
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
        (await client.GetAsync($"/api/projects/{created.Project.Id}/context-browser/files?showTechnical=false&showExports=false")).EnsureSuccessStatusCode();
        (await client.GetAsync($"/api/projects/{created.Project.Id}/context-browser/files?showTechnical=true&showExports=true")).EnsureSuccessStatusCode();
        (await client.GetAsync($"/api/projects/{created.Project.Id}/context-browser/search?query=Sample")).EnsureSuccessStatusCode();
        (await client.GetAsync($"/api/projects/{created.Project.Id}/outputs")).EnsureSuccessStatusCode();
        (await client.GetAsync($"/api/projects/{created.Project.Id}/packs")).EnsureSuccessStatusCode();
        (await client.GetAsync($"/api/projects/{created.Project.Id}/packs/backend-docs")).EnsureSuccessStatusCode();
        (await client.GetAsync($"/api/projects/{created.Project.Id}/packs/backend-docs/content")).EnsureSuccessStatusCode();
        (await client.PostAsync($"/api/projects/{created.Project.Id}/exports/backend-docs-compact/run", null)).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Ai_endpoints_require_token()
    {
        await using var factory = TestApiFactory.Create(requireToken: true);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/ai/providers");

        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Ai_endpoints_configure_preview_and_create_run()
    {
        var root = Path.Combine(Path.GetTempPath(), "sourceworker-api-ai-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(Path.Combine(root, "README.md"), "# Sample");

        await using var factory = TestApiFactory.Create(requireToken: false);
        using var client = factory.CreateClient();
        var created = await (await client.PostAsJsonAsync("/api/projects", new AddProjectRequest(root, "AiSample"))).Content.ReadFromJsonAsync<CreateProjectResponse>();
        Assert.NotNull(created);

        (await client.GetAsync("/api/ai/providers")).EnsureSuccessStatusCode();
        var configure = await client.PostAsJsonAsync("/api/ai/providers/configure", new { kind = 1, model = "llama3.1", baseUrl = "http://localhost:11434", apiKey = (string?)null, isEnabled = true });
        configure.EnsureSuccessStatusCode();
        (await client.PostAsJsonAsync("/api/ai/providers/validate", new { kind = 1, model = "llama3.1", baseUrl = "http://localhost:11434", apiKey = (string?)null, maxOutputTokens = 100 })).EnsureSuccessStatusCode();
        (await client.PostAsync("/api/ai/providers/Ollama/test", null)).EnsureSuccessStatusCode();
        (await client.PostAsync("/api/ai/providers/Ollama/diagnose", null)).EnsureSuccessStatusCode();
        (await client.GetAsync("/api/ai/settings")).EnsureSuccessStatusCode();
        (await client.PutAsJsonAsync("/api/ai/settings", new { storeFullPrompt = false, storeOutput = true, maxRunsPerProject = 50, deleteFailedRunsAfterDays = 14 })).EnsureSuccessStatusCode();
        (await client.GetAsync("/api/ai/usage")).EnsureSuccessStatusCode();
        (await client.GetAsync($"/api/projects/{created!.Project.Id}/ai/packs")).EnsureSuccessStatusCode();
        (await client.GetAsync($"/api/projects/{created.Project.Id}/ai/packs/ai-handoff/quality")).EnsureSuccessStatusCode();
        var preview = await client.PostAsJsonAsync($"/api/projects/{created.Project.Id}/ai/prompt-preview", new { providerId = "Ollama", packId = "ai-handoff", userInstruction = "Summarize", maxOutputTokens = 100 });
        preview.EnsureSuccessStatusCode();
        var run = await client.PostAsJsonAsync($"/api/projects/{created.Project.Id}/ai/runs", new { providerId = "Ollama", packId = "ai-handoff", userInstruction = "Summarize", confirmSafetyWarnings = true, maxOutputTokens = 10 });
        run.EnsureSuccessStatusCode();
        (await client.GetAsync($"/api/projects/{created.Project.Id}/ai/runs")).EnsureSuccessStatusCode();
        (await client.GetAsync($"/api/projects/{created.Project.Id}/ai/usage")).EnsureSuccessStatusCode();
        (await client.GetAsync("/api/dogfood/status")).EnsureSuccessStatusCode();
        (await client.GetAsync($"/api/projects/{created.Project.Id}/dogfood/status")).EnsureSuccessStatusCode();
        (await client.GetAsync("/api/diagnostics/summary")).EnsureSuccessStatusCode();
        (await client.PostAsync("/api/diagnostics/export", null)).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Dogfood_endpoints_work()
    {
        var root = Path.Combine(Path.GetTempPath(), "sourceworker-api-dogfood-" + Guid.NewGuid());
        Directory.CreateDirectory(root);

        await using var factory = TestApiFactory.Create(requireToken: false);
        using var client = factory.CreateClient();

        (await client.PostAsJsonAsync("/api/projects/validate-path", new { path = root })).EnsureSuccessStatusCode();
        (await client.GetAsync("/api/projects/recent-paths")).EnsureSuccessStatusCode();
        var sessionResponse = await client.PostAsJsonAsync("/api/dogfood/sessions", new { projects = new[] { new { projectId = (Guid?)null, name = "SourceWorker", path = (string?)null, isRegistered = false, isScanned = false } } });
        sessionResponse.EnsureSuccessStatusCode();
        var session = await sessionResponse.Content.ReadFromJsonAsync<DogfoodSession>();
        Assert.NotNull(session);
        (await client.GetAsync("/api/dogfood/sessions")).EnsureSuccessStatusCode();
        (await client.GetAsync($"/api/dogfood/sessions/{session!.Id}")).EnsureSuccessStatusCode();
        (await client.PostAsync($"/api/dogfood/sessions/{session.Id}/steps/choose-projects/complete", null)).EnsureSuccessStatusCode();
        (await client.PostAsync($"/api/dogfood/sessions/{session.Id}/finish", null)).EnsureSuccessStatusCode();
        (await client.PostAsync($"/api/dogfood/sessions/{session.Id}/export", null)).EnsureSuccessStatusCode();
        var issue = new DogfoodSessionIssue(Guid.NewGuid(), session.Id, null, "/dogfood", DogfoodFrictionCategory.Other, DogfoodSeverity.Low, "Tiny friction", "Something was odd.", null, null, "Open", DateTimeOffset.UtcNow);
        var issueResponse = await client.PostAsJsonAsync("/api/dogfood/issues", issue);
        issueResponse.EnsureSuccessStatusCode();
        (await client.GetAsync("/api/dogfood/issues")).EnsureSuccessStatusCode();
        (await client.PutAsJsonAsync($"/api/dogfood/issues/{issue.Id}/status", new { status = "Fixed" })).EnsureSuccessStatusCode();
        (await client.PostAsync("/api/dogfood/issues/export", null)).EnsureSuccessStatusCode();
        (await client.GetAsync("/api/next-actions")).EnsureSuccessStatusCode();
        (await client.GetAsync("/api/logs")).EnsureSuccessStatusCode();
    }
}
