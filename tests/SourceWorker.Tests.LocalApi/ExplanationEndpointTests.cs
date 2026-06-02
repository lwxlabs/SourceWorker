using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using SourceWorker.Application;
using SourceWorker.Domain;

namespace SourceWorker.Tests.LocalApi;

public sealed class ExplanationEndpointTests
{
    [Fact]
    public async Task Explanation_endpoints_require_auth()
    {
        await using var factory = TestApiFactory.Create(requireToken: true);
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/projects/{Guid.NewGuid()}/ai/explanation/options");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Explanation_preview_endpoint_builds_prompt_without_provider_call()
    {
        await using var temp = new TempProject();
        temp.Write(".ai-context/agent-handoff.md", "# Agent Handoff\n\nUseful handoff.");
        temp.Write(".ai-context/docs/backend/02-architecture.md", "# Architecture\n\nUseful architecture.");
        temp.Write("README.md", "# Sample\n\nSample project.");
        await using var factory = TestApiFactory.Create(requireToken: false);
        using var client = factory.CreateClient();
        var repo = factory.Services.GetRequiredService<IProjectRepository>();
        var config = factory.Services.GetRequiredService<IAiProviderConfigurationRepository>();
        var project = await repo.AddAsync(new Project(Guid.NewGuid(), "Sample", temp.Root, DateTimeOffset.UtcNow), CancellationToken.None);
        await config.SaveAsync(new AiProviderConfiguration(Guid.NewGuid(), AiProviderKind.Ollama, "Ollama", "llama3.1", "http://localhost:11434", true, null, null, DateTimeOffset.UtcNow), CancellationToken.None);

        var request = new AiExplanationRequest(project.Id, "Ollama", "llama3.1", "backend-docs", ExplanationProfile.FullProjectExplanation, "Explain", false, false, true, false, true, true, 60000, 1200, false, true);
        var response = await client.PostAsJsonAsync($"/api/projects/{project.Id}/ai/explanation/preview", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var preview = await response.Content.ReadFromJsonAsync<AiExplanationPreview>();
        Assert.NotNull(preview);
        Assert.Contains("AI Project Explanation", preview!.PromptText);
        Assert.Contains("Useful handoff", preview.PromptText);
    }

    private sealed class TempProject : IAsyncDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), $"sourceworker-localapi-explanation-{Guid.NewGuid():n}");
        public TempProject() => Directory.CreateDirectory(Root);
        public void Write(string relativePath, string content)
        {
            var path = Path.Combine(Root, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
        }
        public ValueTask DisposeAsync()
        {
            try { Directory.Delete(Root, recursive: true); } catch { }
            return ValueTask.CompletedTask;
        }
    }
}
