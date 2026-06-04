using SourceWorker.AI;
using SourceWorker.Application;
using SourceWorker.Domain;
using System.Runtime.CompilerServices;

namespace SourceWorker.Tests.Application;

public sealed class ExplanationTests
{
    [Fact]
    public async Task Explanation_prompt_includes_context_and_source_without_generated_metadata()
    {
        using var temp = new TempProject();
        temp.Write(".ai-context/agent-handoff.md", "<!-- SourceWorker:Generated; ArtifactId=abc; Status=Generated -->\n# Agent Handoff\n\nUseful handoff.");
        temp.Write(".ai-context/docs/backend/02-architecture.md", "# Architecture\n\nLibrary structure.");
        temp.Write("README.md", "# Sample\n\nSample project.");
        temp.Write("src/PublicApi.cs", "namespace Sample;\npublic sealed class PublicApi\n{\n    public Task RunAsync() => Task.CompletedTask;\n}");
        var project = new Project(Guid.NewGuid(), "Sample", temp.Root, DateTimeOffset.UtcNow);
        var builder = new AiExplanationPromptBuilder(
            new ProjectRepo(project),
            new PackRepo(),
            new ConfigRepo(),
            new StaticAiModelCatalogService(),
            new SimpleAiTokenEstimator(),
            new AiSafetyService(),
            new SymbolRepo(project.Id, "src/PublicApi.cs"),
            new TestRepo());

        var preview = await builder.BuildPreviewAsync(Request(project.Id), CancellationToken.None);

        Assert.Contains("Agent Handoff", preview.PromptText);
        Assert.Contains("Library structure", preview.PromptText);
        Assert.Contains("PublicApi", preview.PromptText);
        Assert.DoesNotContain("SourceWorker:Generated", preview.PromptText);
        Assert.Contains(preview.IncludedFiles, f => f.RelativePath == "src/PublicApi.cs" && f.Included);
    }

    [Fact]
    public async Task Explanation_run_and_save_writes_only_generated_explanation_file()
    {
        using var temp = new TempProject();
        var project = new Project(Guid.NewGuid(), "Sample", temp.Root, DateTimeOffset.UtcNow);
        var runs = new RunRepo();
        var artifacts = new ArtifactRepo();
        var service = new AiExplanationService(
            new ProjectRepo(project),
            new PackRepo(),
            new ConfigRepo(),
            new ProviderRegistry(),
            new PromptBuilder(),
            new AiExplanationQualityService(),
            runs,
            artifacts,
            new ScanRepo(project.Id));

        var result = await service.RunAsync(Request(project.Id), CancellationToken.None);
        var save = await service.SaveAsync(new AiExplanationSaveRequest(project.Id, result.Id), CancellationToken.None);

        Assert.Equal(AiRunStatus.Completed, result.Status);
        Assert.True(save.Saved);
        Assert.Equal(".ai-context/docs/backend/07-ai-explanation.md", save.SuggestedFilePath);
        Assert.True(File.Exists(Path.Combine(temp.Root, ".ai-context", "docs", "backend", "07-ai-explanation.md")));
        Assert.False(File.Exists(Path.Combine(temp.Root, "src", "PublicApi.cs")));
        Assert.Contains(artifacts.Items, a => a.RelativePath == ".ai-context/docs/backend/07-ai-explanation.md" && a.Type == GeneratedArtifactType.InternalGeneratedDoc);
    }

    [Fact]
    public async Task Explanation_run_auto_saves_generated_explanation_file()
    {
        using var temp = new TempProject();
        var project = new Project(Guid.NewGuid(), "Sample", temp.Root, DateTimeOffset.UtcNow);
        var service = new AiExplanationService(
            new ProjectRepo(project),
            new PackRepo(),
            new ConfigRepo(),
            new ProviderRegistry(),
            new PromptBuilder(),
            new AiExplanationQualityService(),
            new RunRepo(),
            new ArtifactRepo(),
            new ScanRepo(project.Id));

        var result = await service.RunAsync(Request(project.Id), CancellationToken.None);

        Assert.Equal(AiRunStatus.Completed, result.Status);
        Assert.True(File.Exists(Path.Combine(temp.Root, ".ai-context", "docs", "backend", "07-ai-explanation.md")));
    }

    [Fact]
    public async Task Explanation_stream_auto_saves_generated_explanation_file()
    {
        using var temp = new TempProject();
        var project = new Project(Guid.NewGuid(), "Sample", temp.Root, DateTimeOffset.UtcNow);
        var artifacts = new ArtifactRepo();
        var service = new AiExplanationService(
            new ProjectRepo(project),
            new PackRepo(),
            new ConfigRepo(),
            new ProviderRegistry(new StreamingProvider()),
            new PromptBuilder(),
            new AiExplanationQualityService(),
            new RunRepo(),
            artifacts,
            new ScanRepo(project.Id));

        var updates = new List<AiExplanationStreamUpdate>();
        await foreach (var update in service.RunStreamingAsync(Request(project.Id), CancellationToken.None))
        {
            updates.Add(update);
        }

        Assert.Contains(updates, update => update.Phase == "saved");
        Assert.Contains(updates, update => update.IsCompleted && update.Result?.Status == AiRunStatus.Completed);
        Assert.True(File.Exists(Path.Combine(temp.Root, ".ai-context", "docs", "backend", "07-ai-explanation.md")));
        Assert.Contains(artifacts.Items, a => a.RelativePath == ".ai-context/docs/backend/07-ai-explanation.md");
    }

    [Fact]
    public async Task Explanation_run_fails_when_provider_returns_empty_text()
    {
        using var temp = new TempProject();
        var project = new Project(Guid.NewGuid(), "Sample", temp.Root, DateTimeOffset.UtcNow);
        var runs = new RunRepo();
        var service = new AiExplanationService(
            new ProjectRepo(project),
            new PackRepo(),
            new ConfigRepo(),
            new ProviderRegistry(new EmptyProvider()),
            new PromptBuilder(),
            new AiExplanationQualityService(),
            runs,
            new ArtifactRepo(),
            new ScanRepo(project.Id));

        var result = await service.RunAsync(Request(project.Id), CancellationToken.None);

        Assert.Equal(AiRunStatus.Failed, result.Status);
        Assert.Contains("returned no explanation text", result.Error);
        Assert.True(string.IsNullOrWhiteSpace(result.Markdown));
    }

    [Fact]
    public void Explanation_quality_checker_fails_short_output_and_accepts_complete_output()
    {
        var checker = new AiExplanationQualityService();

        var shortResult = checker.Check("# AI Project Explanation\n\nToo short.");
        var complete = checker.Check("""
            # AI Project Explanation

            ## 1. What This Project Is
            This project is explained with enough concrete detail for a developer.
            ## 2. Mental Model
            The mental model is described from supplied context.
            ## 3. Solution / Project Structure
            The solution and project structure are covered.
            ## 4. Main Runtime Flow
            Runtime flow is explained without patches.
            ## 5. Public API / Main Entry Points
            Public API and entry points are named from supplied context.
            ## 6. Data / Storage / External Systems
            Storage and external systems are covered or marked unknown.
            ## 7. Configuration
            Configuration is covered.
            ## 8. Tests And Quality
            Tests and quality are covered.
            ## 9. Important Files To Read First
            A reading order is included.
            ## 10. Risks / Unknowns
            Real unknowns are listed.
            ## 11. How An AI Coding Agent Should Continue
            The agent should inspect files first, preserve architecture, and run targeted tests.
            This deliberately includes enough extra explanation text to avoid the too-short quality warning and represents a complete durable explanation.
            """);

        Assert.Equal(AiExplanationQualityStatus.Failed, shortResult.Status);
        Assert.Equal(AiExplanationQualityStatus.Good, complete.Status);
    }

    private static AiExplanationRequest Request(Guid projectId)
        => new(projectId, "Ollama", "llama3.1", "backend-docs", ExplanationProfile.FullProjectExplanation, "Explain clearly.", true, true, true, true, true, true, 60000, 1200, false, true);

    private sealed class TempProject : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), $"sourceworker-explanation-{Guid.NewGuid():n}");
        public TempProject() => Directory.CreateDirectory(Root);
        public void Write(string relativePath, string content)
        {
            var path = Path.Combine(Root, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
        }
        public void Dispose() { try { Directory.Delete(Root, recursive: true); } catch { } }
    }

    private sealed class ProjectRepo(Project project) : IProjectRepository
    {
        public Task<Project> AddAsync(Project p, CancellationToken cancellationToken) => Task.FromResult(p);
        public Task DeleteAsync(Guid id, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<Project?> GetAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult<Project?>(id == project.Id ? project : null);
        public Task<Project?> GetByPathAsync(string rootPath, CancellationToken cancellationToken) => Task.FromResult<Project?>(project);
        public Task<IReadOnlyList<Project>> ListAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Project>>([project]);
        public Task<Project> UpdateAsync(Project p, CancellationToken cancellationToken) => Task.FromResult(p);
    }

    private sealed class PackRepo : IContextPackService
    {
        public Task<ContextPackManifest?> GetAsync(Project project, string packId, CancellationToken cancellationToken)
            => Task.FromResult<ContextPackManifest?>(new ContextPackManifest(new ContextPack(packId, "Backend Docs", "Docs", "developer", "Explain", [], [], "compact", 0, "fresh", null, DateTimeOffset.UtcNow), ".ai-context/packs/backend-docs.pack.yaml", [], true));
        public Task<IReadOnlyList<ContextPackManifest>> ListAsync(Project project, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<ContextPackManifest>>([(GetAsync(project, "backend-docs", cancellationToken).Result)!]);
    }

    private sealed class ConfigRepo : IAiProviderConfigurationRepository
    {
        public Task<AiProviderConfiguration?> GetAsync(string providerId, CancellationToken cancellationToken) => Task.FromResult<AiProviderConfiguration?>(new AiProviderConfiguration(Guid.NewGuid(), AiProviderKind.Ollama, "Ollama", "llama3.1", "http://localhost:11434", true, null, null, DateTimeOffset.UtcNow));
        public Task<IReadOnlyList<AiProviderConfiguration>> ListAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<AiProviderConfiguration>>([]);
        public Task<AiProviderConfiguration> SaveAsync(AiProviderConfiguration configuration, CancellationToken cancellationToken) => Task.FromResult(configuration);
    }

    private sealed class SymbolRepo(Guid projectId, string relativePath) : ICodeSymbolRepository
    {
        public Task<IReadOnlyList<CodeSymbol>> GetByProjectAsync(Guid id, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<CodeSymbol>>([new CodeSymbol(Guid.NewGuid(), projectId, Guid.NewGuid(), relativePath, relativePath, "Sample", "PublicApi", "Sample.PublicApi", CodeSymbolKind.Class, "public", null, null, [], 1, [], false, false)]);
        public Task SaveAsync(Guid projectId, Guid scanRunId, IReadOnlyList<CodeSymbol> symbols, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class TestRepo : ITestInfoRepository
    {
        public Task<IReadOnlyList<TestInfo>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<TestInfo>>([]);
        public Task SaveAsync(Guid projectId, Guid scanRunId, IReadOnlyList<TestInfo> tests, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class ProviderRegistry(IAiProvider? configuredProvider = null) : IAiProviderRegistry
    {
        private readonly IAiProvider provider = configuredProvider ?? new Provider();
        public IAiProvider? GetProvider(string providerId) => provider;
        public IReadOnlyList<IAiProvider> ListProviders() => [provider];
    }

    private sealed class Provider : IAiProvider
    {
        public string Id => "Ollama";
        public AiProviderKind Kind => AiProviderKind.Ollama;
        public Task<AiProviderHealthResult> CheckHealthAsync(AiProviderConfiguration configuration, CancellationToken cancellationToken) => Task.FromResult(new AiProviderHealthResult(Id, Kind, AiProviderStatus.Healthy, "ok", DateTimeOffset.UtcNow));
        public Task<AiRunOutput> RunAsync(AiProviderConfiguration configuration, string prompt, int maxOutputTokens, CancellationToken cancellationToken)
            => Task.FromResult(new AiRunOutput(CompleteMarkdown, "output-hash"));
    }

    private sealed class StreamingProvider : IAiStreamingProvider
    {
        public string Id => "Ollama";
        public AiProviderKind Kind => AiProviderKind.Ollama;
        public Task<AiProviderHealthResult> CheckHealthAsync(AiProviderConfiguration configuration, CancellationToken cancellationToken) => Task.FromResult(new AiProviderHealthResult(Id, Kind, AiProviderStatus.Healthy, "ok", DateTimeOffset.UtcNow));
        public Task<AiRunOutput> RunAsync(AiProviderConfiguration configuration, string prompt, int maxOutputTokens, CancellationToken cancellationToken)
            => Task.FromResult(new AiRunOutput(CompleteMarkdown, "output-hash"));

        public async IAsyncEnumerable<AiProviderStreamUpdate> StreamAsync(AiProviderConfiguration configuration, string prompt, int maxOutputTokens, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.Yield();
            yield return new AiProviderStreamUpdate(CompleteMarkdown[..80], "Writing answer...", 80, DateTimeOffset.UtcNow);
            yield return new AiProviderStreamUpdate(CompleteMarkdown[80..], "Writing answer...", CompleteMarkdown.Length, DateTimeOffset.UtcNow);
            yield return new AiProviderStreamUpdate(null, "Provider completed.", CompleteMarkdown.Length, DateTimeOffset.UtcNow, true);
        }
    }

    private sealed class EmptyProvider : IAiProvider
    {
        public string Id => "Ollama";
        public AiProviderKind Kind => AiProviderKind.Ollama;
        public Task<AiProviderHealthResult> CheckHealthAsync(AiProviderConfiguration configuration, CancellationToken cancellationToken) => Task.FromResult(new AiProviderHealthResult(Id, Kind, AiProviderStatus.Healthy, "ok", DateTimeOffset.UtcNow));
        public Task<AiRunOutput> RunAsync(AiProviderConfiguration configuration, string prompt, int maxOutputTokens, CancellationToken cancellationToken)
            => Task.FromResult(new AiRunOutput("", "empty-output-hash"));
    }

    private sealed class PromptBuilder : IAiExplanationPromptBuilder
    {
        public Task<AiExplanationPreview> BuildPreviewAsync(AiExplanationRequest request, CancellationToken cancellationToken)
            => Task.FromResult(new AiExplanationPreview(request.ProjectId, request.ProviderId, AiProviderKind.Ollama, request.Model, request.ContextPackId, request.ExplanationProfile, [], [], [], new AiTokenEstimate(10, request.MaxOutputTokens, 10 + request.MaxOutputTokens), new AiCostEstimate(null, null, null), "prompt", "prompt-hash", false));
    }

    private sealed class RunRepo : IAiRunRepository
    {
        private readonly Dictionary<Guid, AiRun> runs = [];
        public Task DeleteAsync(Guid runId, CancellationToken cancellationToken) { runs.Remove(runId); return Task.CompletedTask; }
        public Task DeleteByProjectAsync(Guid projectId, CancellationToken cancellationToken) { foreach (var id in runs.Values.Where(r => r.ProjectId == projectId).Select(r => r.Id).ToArray()) runs.Remove(id); return Task.CompletedTask; }
        public Task<AiRun?> GetAsync(Guid runId, CancellationToken cancellationToken) => Task.FromResult(runs.GetValueOrDefault(runId));
        public Task<IReadOnlyList<AiRun>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<AiRun>>(runs.Values.Where(r => r.ProjectId == projectId).ToArray());
        public Task<AiRun> SaveAsync(AiRun run, CancellationToken cancellationToken) { runs[run.Id] = run; return Task.FromResult(run); }
    }

    private sealed class ArtifactRepo : IGeneratedArtifactRepository
    {
        public List<GeneratedArtifact> Items { get; } = [];
        public Task<IReadOnlyList<GeneratedArtifact>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<GeneratedArtifact>>(Items.Where(i => i.ProjectId == projectId).ToArray());
        public Task<GeneratedArtifact?> GetByPathAsync(Guid projectId, string relativePath, CancellationToken cancellationToken) => Task.FromResult(Items.FirstOrDefault(i => i.ProjectId == projectId && i.RelativePath == relativePath));
        public Task<GeneratedArtifact> UpsertAsync(GeneratedArtifact artifact, CancellationToken cancellationToken) { Items.RemoveAll(i => i.ProjectId == artifact.ProjectId && i.RelativePath == artifact.RelativePath); Items.Add(artifact); return Task.FromResult(artifact); }
    }

    private sealed class ScanRepo(Guid projectId) : IScanRunRepository
    {
        private readonly ScanRun scan = new(Guid.NewGuid(), projectId, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, ScanRunStatus.Completed, 1, 0);
        public Task<ScanRun> AddAsync(ScanRun scanRun, CancellationToken cancellationToken) => Task.FromResult(scanRun);
        public Task<ScanRun?> GetAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult<ScanRun?>(scan);
        public Task<IReadOnlyList<ScanRun>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ScanRun>>([scan]);
        public Task<ScanRun?> GetLatestAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult<ScanRun?>(scan);
        public Task<ScanRun> UpdateAsync(ScanRun scanRun, CancellationToken cancellationToken) => Task.FromResult(scanRun);
    }

    private const string CompleteMarkdown = """
        # AI Project Explanation

        ## 1. What This Project Is
        A sample project.
        ## 2. Mental Model
        Think about it as a sample.
        ## 3. Solution / Project Structure
        It has a project and source files.
        ## 4. Main Runtime Flow
        The runtime flow is simple.
        ## 5. Public API / Main Entry Points
        PublicApi is the main entry point.
        ## 6. Data / Storage / External Systems
        Unknown from supplied context.
        ## 7. Configuration
        Unknown from supplied context.
        ## 8. Tests And Quality
        Tests are described by supplied context.
        ## 9. Important Files To Read First
        Read the handoff and public API first.
        ## 10. Risks / Unknowns
        Unknowns are explicitly noted.
        ## 11. How An AI Coding Agent Should Continue
        Inspect files first and run targeted tests.
        """;
}
