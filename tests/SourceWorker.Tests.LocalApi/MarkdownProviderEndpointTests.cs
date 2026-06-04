using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using SourceWorker.Application;
using SourceWorker.Domain;

namespace SourceWorker.Tests.LocalApi;

public sealed class MarkdownProviderEndpointTests
{
    [Fact]
    public async Task Markdown_endpoints_require_auth()
    {
        await using var factory = TestApiFactory.Create(requireToken: true);
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/projects/{Guid.NewGuid()}/markdown/files");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Markdown_read_and_save_work_for_markdown_and_reject_source_extension()
    {
        await using var temp = new TempProject();
        temp.Write("README.md", "# Readme");
        await using var factory = TestApiFactory.Create(requireToken: false);
        using var client = factory.CreateClient();
        var repo = factory.Services.GetRequiredService<IProjectRepository>();
        var project = await repo.AddAsync(new Project(Guid.NewGuid(), "Sample", temp.Root, DateTimeOffset.UtcNow), CancellationToken.None);

        var files = await client.GetFromJsonAsync<MarkdownFileSummary[]>($"/api/projects/{project.Id}/markdown/files?includeUserMarkdown=true");
        var content = await client.GetFromJsonAsync<MarkdownFileContent>($"/api/projects/{project.Id}/markdown/files/README.md");
        var saved = await (await client.PutAsJsonAsync($"/api/projects/{project.Id}/markdown/files/README.md", new MarkdownFileSaveRequest("README.md", "# Edited", content!.ContentHash))).Content.ReadFromJsonAsync<MarkdownFileSaveResult>();
        var rejected = await client.PutAsJsonAsync($"/api/projects/{project.Id}/markdown/files/Program.cs", new MarkdownFileSaveRequest("Program.cs", "code", null));

        Assert.NotNull(files);
        Assert.Contains(files!, f => f.RelativePath == "README.md");
        Assert.True(saved!.Saved);
        Assert.Equal(HttpStatusCode.InternalServerError, rejected.StatusCode);
    }

    [Fact]
    public async Task Provider_presets_endpoint_returns_expected_presets()
    {
        await using var factory = TestApiFactory.Create(requireToken: false);
        using var client = factory.CreateClient();

        var presets = await client.GetFromJsonAsync<AiProviderPresetInfo[]>("/api/ai/providers/presets");

        Assert.NotNull(presets);
        Assert.Contains(presets!, p => p.Preset == ProviderPreset.OpenAI);
        Assert.Contains(presets!, p => p.Preset == ProviderPreset.Gemini);
        Assert.Contains(presets!, p => p.Preset == ProviderPreset.LMStudio);
        Assert.Contains(presets!, p => p.Preset == ProviderPreset.Ollama);
    }

    private sealed class TempProject : IAsyncDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), $"sourceworker-localapi-markdown-{Guid.NewGuid():n}");
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
