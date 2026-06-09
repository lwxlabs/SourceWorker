using SourceWorker.Application;
using SourceWorker.Context;
using SourceWorker.Domain;

namespace SourceWorker.Tests.Application;

public sealed class MarkdownTests
{
    [Fact]
    public async Task Markdown_list_includes_backend_docs_and_readme_but_excludes_yaml()
    {
        using var workspace = new TempWorkspace();
        await workspace.WriteAsync("README.md", "# Readme");
        await workspace.WriteAsync(".ai-context/docs/backend/07-ai-explanation.md", "# Explanation");
        await workspace.WriteAsync(".ai-context/packs/ai-handoff.pack.yaml", "id: ai-handoff");
        await workspace.WriteAsync(".ai-context/index.yaml", "formatVersion: 5");
        var service = workspace.CreateService();

        var files = await service.ListFilesAsync(workspace.Project, null, includeTechnical: true, includeExternalMirror: false, includeUserMarkdown: true, CancellationToken.None);

        Assert.Contains(files, file => file.RelativePath == "README.md");
        Assert.Contains(files, file => file.RelativePath == ".ai-context/docs/backend/07-ai-explanation.md");
        Assert.DoesNotContain(files, file => file.RelativePath.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Markdown_read_blocks_path_traversal()
    {
        using var workspace = new TempWorkspace();
        var service = workspace.CreateService();

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ReadAsync(workspace.Project, "../outside.md", CancellationToken.None));
    }

    [Fact]
    public async Task Markdown_save_updates_only_markdown_and_blocks_source_or_yaml()
    {
        using var workspace = new TempWorkspace();
        await workspace.WriteAsync("notes.md", "old");
        var service = workspace.CreateService();
        var loaded = await service.ReadAsync(workspace.Project, "notes.md", CancellationToken.None);

        var saved = await service.SaveAsync(workspace.Project, new MarkdownFileSaveRequest("notes.md", "new", loaded.ContentHash), CancellationToken.None);

        Assert.True(saved.Saved);
        Assert.Equal("new", await File.ReadAllTextAsync(Path.Combine(workspace.Root, "notes.md")));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveAsync(workspace.Project, new MarkdownFileSaveRequest("Program.cs", "code", null), CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveAsync(workspace.Project, new MarkdownFileSaveRequest(".ai-context/index.yaml", "yaml", null), CancellationToken.None));
    }

    [Fact]
    public async Task Markdown_save_generated_manual_file_requires_confirmation()
    {
        using var workspace = new TempWorkspace();
        await workspace.WriteAsync(".ai-context/docs/backend/07-ai-explanation.md", "old");
        var artifact = new GeneratedArtifact(Guid.NewGuid(), workspace.Project.Id, null, "ai-explanation", ".ai-context/docs/backend/07-ai-explanation.md", Path.Combine(workspace.Root, ".ai-context/docs/backend/07-ai-explanation.md"), GeneratedArtifactType.InternalGeneratedDoc, "oldhash", DateTimeOffset.UtcNow, "source", GeneratedArtifactStatus.ManuallyModified, "test", null);
        workspace.Artifacts.Items.Add(artifact);
        var service = workspace.CreateService();

        var result = await service.SaveAsync(workspace.Project, new MarkdownFileSaveRequest(".ai-context/docs/backend/07-ai-explanation.md", "new", null), CancellationToken.None);

        Assert.False(result.Saved);
        Assert.True(result.RequiresConfirmation);
    }

    [Fact]
    public void Markdown_mermaid_validator_accepts_valid_fenced_flowchart()
    {
        var result = new MermaidMarkdownValidator().Validate("""
```mermaid
flowchart LR
    A[Write Markdown] --> B[Preview]
```
""");

        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Markdown_mermaid_validator_warns_on_unfenced_or_invalid_mermaid()
    {
        var validator = new MermaidMarkdownValidator();

        Assert.Contains(validator.Validate("flowchart TD\nA[Start] --> B[End]").Warnings, w => w.Contains("fenced", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(validator.Validate("""
```mermaid
notAChart
```
""").Warnings, w => w.Contains("valid diagram", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Ai_snippet_ignore_rules_remove_noisy_lines_only()
    {
        var rules = """
            enabled: true
            contains: <!-- SourceWorker:Generated
            contains: Completed at:
            regex: ^\s*Started:
            """;
        var text = """
            <!-- SourceWorker:Generated; ArtifactId=abc; Status=Generated -->
            # Useful Context
            Started: yesterday
            Completed at: later
            Keep this line.
            """;

        var filtered = AiIgnoreRules.ApplySnippetIgnores(rules, text);

        Assert.Contains("Keep this line.", filtered);
        Assert.DoesNotContain("SourceWorker:Generated", filtered);
        Assert.DoesNotContain("Started:", filtered);
        Assert.DoesNotContain("Completed at:", filtered);
    }

    private sealed class TempWorkspace : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "sourceworker-markdown-" + Guid.NewGuid());
        public Project Project { get; }
        public ArtifactRepo Artifacts { get; } = new();

        public TempWorkspace()
        {
            Directory.CreateDirectory(Root);
            Project = new Project(Guid.NewGuid(), "Markdown", Root, DateTimeOffset.UtcNow);
        }

        public async Task WriteAsync(string relativePath, string content)
        {
            var path = Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(path, content);
        }

        public MarkdownWorkspaceService CreateService() => new(Artifacts, new MermaidMarkdownValidator());

        public void Dispose()
        {
            if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true);
        }
    }

    private sealed class ArtifactRepo : IGeneratedArtifactRepository
    {
        public List<GeneratedArtifact> Items { get; } = [];
        public Task<GeneratedArtifact> UpsertAsync(GeneratedArtifact artifact, CancellationToken cancellationToken)
        {
            Items.RemoveAll(item => item.Id == artifact.Id || item.RelativePath.Equals(artifact.RelativePath, StringComparison.OrdinalIgnoreCase));
            Items.Add(artifact);
            return Task.FromResult(artifact);
        }
        public Task<IReadOnlyList<GeneratedArtifact>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<GeneratedArtifact>>(Items.Where(a => a.ProjectId == projectId).ToArray());
        public Task<GeneratedArtifact?> GetByPathAsync(Guid projectId, string relativePath, CancellationToken cancellationToken) => Task.FromResult(Items.FirstOrDefault(a => a.ProjectId == projectId && a.RelativePath.Equals(relativePath, StringComparison.OrdinalIgnoreCase)));
    }
}
