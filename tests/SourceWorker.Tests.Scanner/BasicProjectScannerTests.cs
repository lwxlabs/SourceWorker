using SourceWorker.Application;
using SourceWorker.Context;
using SourceWorker.Domain;
using SourceWorker.Recipes;
using SourceWorker.Scanner;

namespace SourceWorker.Tests.Scanner;

public sealed class BasicProjectScannerTests
{
    [Fact]
    public async Task ScanAsync_detects_dotnet_files_todos_and_ignores_skipped_directories()
    {
        var root = Path.Combine(Path.GetTempPath(), "sourceworker-scanner-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(Path.Combine(root, "src"));
        Directory.CreateDirectory(Path.Combine(root, "bin"));
        await File.WriteAllTextAsync(Path.Combine(root, "Sample.sln"), "");
        await File.WriteAllTextAsync(Path.Combine(root, "src", "Sample.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk.Web\"></Project>");
        await File.WriteAllTextAsync(Path.Combine(root, "src", "Program.cs"), "// TODO: wire app");
        await File.WriteAllTextAsync(Path.Combine(root, "README.md"), "# Sample");
        await File.WriteAllTextAsync(Path.Combine(root, "bin", "Ignored.cs"), "// FIXME: no");

        var project = new Project(Guid.NewGuid(), "Sample", root, DateTimeOffset.UtcNow);
        var result = await new BasicProjectScanner(new EmptyGitInspector()).ScanAsync(project, CancellationToken.None);

        Assert.Contains(result.Files, file => file.Kind == IndexedFileKind.Solution);
        Assert.Contains(result.Files, file => file.Kind == IndexedFileKind.Project);
        Assert.Contains(result.SolutionFiles, solution => solution.RelativePath == "Sample.sln");
        Assert.Contains(result.Projects, projectFile => projectFile.RelativePath == "src/Sample.csproj");
        Assert.All(result.Files, file => Assert.False(string.IsNullOrWhiteSpace(file.Sha256)));
        Assert.Contains(result.Technologies, tech => tech.Kind == TechnologyKind.DotNet);
        Assert.Contains(result.Technologies, tech => tech.Kind == TechnologyKind.AspNetCore);
        Assert.Single(result.Todos);
        Assert.DoesNotContain(result.Files, file => file.RelativePath.Contains("bin/", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Context_writer_creates_expected_files()
    {
        var root = await CreateSampleProject();
        var project = new Project(Guid.NewGuid(), "Sample", root, DateTimeOffset.UtcNow);
        var scan = await new BasicProjectScanner(new EmptyGitInspector()).ScanAsync(project, CancellationToken.None);

        await new MarkdownContextWriter().WriteBasicContextAsync(scan, CancellationToken.None);

        Assert.True(File.Exists(Path.Combine(root, ".ai-context", "CONTEXT.md")));
        Assert.True(File.Exists(Path.Combine(root, ".ai-context", "index.yaml")));
        Assert.True(File.Exists(Path.Combine(root, ".ai-context", "agent-handoff.md")));
        Assert.True(File.Exists(Path.Combine(root, ".ai-context", "project.md")));
        Assert.True(File.Exists(Path.Combine(root, ".ai-context", "architecture.md")));
        Assert.True(File.Exists(Path.Combine(root, ".ai-context", "current-state.md")));
        Assert.True(File.Exists(Path.Combine(root, ".ai-context", "tasks.md")));
        Assert.True(File.Exists(Path.Combine(root, ".ai-context", "ai-handoff.md")));
        Assert.True(File.Exists(Path.Combine(root, ".ai-context", "scans", "latest-scan.md")));
        Assert.True(File.Exists(Path.Combine(root, ".ai-context", "branches", "current.md")));
        Assert.True(File.Exists(Path.Combine(root, ".ai-context", "instructions", "README.md")));
    }

    [Fact]
    public async Task Handoff_generated_markdown_is_clean_and_agent_focused()
    {
        var root = await CreateSampleProject();
        var project = new Project(Guid.NewGuid(), "Sample", root, DateTimeOffset.UtcNow);
        var scan = await new BasicProjectScanner(new EmptyGitInspector()).ScanAsync(project, CancellationToken.None);

        await new MarkdownContextWriter().WriteBasicContextAsync(scan, CancellationToken.None);

        var handoff = await File.ReadAllTextAsync(Path.Combine(root, ".ai-context", "agent-handoff.md"));
        var projectDoc = await File.ReadAllTextAsync(Path.Combine(root, ".ai-context", "project.md"));

        Assert.Contains("# Agent Handoff", handoff);
        Assert.Contains("Recommended Prompt", handoff);
        Assert.DoesNotContain("GeneratedBy: SourceWorker", handoff);
        Assert.DoesNotContain("FormatVersion: 2", handoff);
        Assert.DoesNotContain("GeneratedBy: SourceWorker", projectDoc);
        Assert.Contains("SourceWorker:Generated", projectDoc);
    }

    [Fact]
    public async Task Scanner_excludes_generated_context_todos_from_source_todos()
    {
        var root = await CreateSampleProject();
        Directory.CreateDirectory(Path.Combine(root, "instructions", "Result", "Backend"));
        await File.WriteAllTextAsync(Path.Combine(root, "instructions", "Result", "Backend", "05-tasks-and-risks.md"), "// TODO: generated context should not become a source todo");

        var project = new Project(Guid.NewGuid(), "Sample", root, DateTimeOffset.UtcNow);
        var scan = await new BasicProjectScanner(new EmptyGitInspector()).ScanAsync(project, CancellationToken.None);

        Assert.DoesNotContain(scan.Todos, todo => todo.RelativePath.StartsWith("instructions/Result/", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(scan.Files, file => file.RelativePath.StartsWith("instructions/Result/", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Library_handoff_infers_purpose_structure_and_domain_flows_without_sourceworker_flows()
    {
        var root = Path.Combine(Path.GetTempPath(), "sourceworker-localite-like-" + Guid.NewGuid());
        Directory.CreateDirectory(Path.Combine(root, "src", "Localite", "Contexts", "Query"));
        Directory.CreateDirectory(Path.Combine(root, "src", "Localite", "Contexts", "Save"));
        Directory.CreateDirectory(Path.Combine(root, "src", "Localite", "Cache"));
        Directory.CreateDirectory(Path.Combine(root, "tests", "Localite.Tests"));
        Directory.CreateDirectory(Path.Combine(root, "instructions", "Result", "Backend"));
        await File.WriteAllTextAsync(Path.Combine(root, "Localite.sln"), "");
        await File.WriteAllTextAsync(Path.Combine(root, "README.md"), "# Localite\n\nLocalite is a local persistence library for .NET applications.");
        await File.WriteAllTextAsync(Path.Combine(root, "src", "Localite", "Localite.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");
        await File.WriteAllTextAsync(Path.Combine(root, "tests", "Localite.Tests", "Localite.Tests.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"><ItemGroup><PackageReference Include=\"xunit\" Version=\"2.0.0\" /></ItemGroup></Project>");
        await File.WriteAllTextAsync(Path.Combine(root, "src", "Localite", "Contexts", "Query", "LocaliteQueryContext.cs"), "namespace Localite.Contexts.Query; public class LocaliteQueryContext { public void QueryData() { } }");
        await File.WriteAllTextAsync(Path.Combine(root, "src", "Localite", "Contexts", "Save", "LocaliteSaveContext.cs"), "namespace Localite.Contexts.Save; public class LocaliteSaveContext { public void SaveGraph() { } }");
        await File.WriteAllTextAsync(Path.Combine(root, "src", "Localite", "Cache", "LocaliteCache.cs"), "namespace Localite.Cache; public class LocaliteCache { public void SetCache() { } public void GetCache() { } }");
        await File.WriteAllTextAsync(Path.Combine(root, "tests", "Localite.Tests", "LocaliteQueryTests.cs"), "public class FakeCustomer { } public class LocaliteQueryTests { [Fact] public void Query_graph_reads_values() { } [Fact] public void Save_graph_writes_values() { } }");
        await File.WriteAllTextAsync(Path.Combine(root, "instructions", "Result", "Backend", "05-tasks-and-risks.md"), "// TODO: generated context should stay out");

        var project = new Project(Guid.NewGuid(), "Localite", root, DateTimeOffset.UtcNow);
        var scan = await new BasicProjectScanner(new EmptyGitInspector()).ScanAsync(project, CancellationToken.None);
        var code = await new CSharpCodeIntelligenceScanner(new RuleBasedFlowExtractor()).ScanAsync(scan, CancellationToken.None);
        var artifacts = new MemoryGeneratedArtifactRepository();
        await new MarkdownContextWriter(
            artifacts: artifacts,
            symbols: new SymbolRepo(code.Symbols),
            flows: new FlowRepo(code.Flows),
            routes: new RouteRepo(code.Routes),
            services: new ServiceRepo(code.ServiceRegistrations),
            configs: new ConfigRepo(code.ConfigurationUsages),
            tests: new TestRepo(code.Tests))
            .WriteBasicContextAsync(scan, CancellationToken.None);

        var handoff = await File.ReadAllTextAsync(Path.Combine(root, ".ai-context", "agent-handoff.md"));

        Assert.Contains("local persistence library", handoff, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Library-style structure", handoff);
        Assert.Contains("Query data", handoff);
        Assert.Contains("Save graph", handoff);
        Assert.Contains("Cache", handoff, StringComparison.OrdinalIgnoreCase);
        Assert.True(handoff.IndexOf("LocaliteQueryContext", StringComparison.OrdinalIgnoreCase) < handoff.IndexOf("FakeCustomer", StringComparison.OrdinalIgnoreCase) || !handoff.Contains("FakeCustomer", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("Scan Project", handoff);
        Assert.DoesNotContain("Artifact Cleanup", handoff);
        Assert.DoesNotContain("instructions/Result/Backend", handoff);
        Assert.DoesNotContain("GeneratedBy: SourceWorker", handoff);
    }

    [Fact]
    public async Task Backend_docs_compact_recipe_creates_expected_files()
    {
        var root = await CreateSampleProject();
        var project = new Project(Guid.NewGuid(), "Sample", root, DateTimeOffset.UtcNow);
        var scanner = new BasicProjectScanner(new EmptyGitInspector());
        var scan = await scanner.ScanAsync(project, CancellationToken.None);

        var artifactRepository = new MemoryGeneratedArtifactRepository();
        await new BasicRecipeRunner(scanner, new MarkdownContextWriter(), new EmptyInstructionRepository(), artifactRepository, exportProfiles: new ExportProfileService(artifactRepository), packWriter: new ContextPackService(artifactRepository)).RunBackendDocsCompactAsync(scan, CancellationToken.None);

        var output = Path.Combine(root, ".ai-context", "docs", "backend");
        var external = Path.Combine(root, "instructions", "Result", "Backend");
        Assert.True(File.Exists(Path.Combine(root, ".ai-context", "recipes", "backend-docs-compact.yaml")));
        Assert.True(File.Exists(Path.Combine(output, "00-project-index.md")));
        Assert.True(File.Exists(Path.Combine(output, "README.md")));
        Assert.True(File.Exists(Path.Combine(output, "01-project-structure.md")));
        Assert.True(File.Exists(Path.Combine(output, "02-architecture.md")));
        Assert.True(File.Exists(Path.Combine(output, "03-important-files-and-flows.md")));
        Assert.True(File.Exists(Path.Combine(output, "04-configuration-and-secrets.md")));
        Assert.True(File.Exists(Path.Combine(output, "05-tasks-and-risks.md")));
        Assert.True(File.Exists(Path.Combine(output, "06-verification-checklist.md")));
        Assert.True(File.Exists(Path.Combine(output, "08-code-snippets.md")));
        Assert.True(File.Exists(Path.Combine(output, "09-reading-guide.md")));
        Assert.True(File.Exists(Path.Combine(external, "00-project-index.md")));
        Assert.True(File.Exists(Path.Combine(external, "README.md")));
        Assert.True(File.Exists(Path.Combine(external, "06-verification-checklist.md")));
        Assert.True(File.Exists(Path.Combine(external, "08-code-snippets.md")));
        Assert.True(File.Exists(Path.Combine(external, "09-reading-guide.md")));
        Assert.True(File.Exists(Path.Combine(root, ".ai-context", "exports", "backend-docs-compact.yaml")));
        Assert.True(File.Exists(Path.Combine(root, ".ai-context", "packs", "backend-docs.pack.yaml")));
        Assert.Contains("00-project-index.md", await File.ReadAllTextAsync(Path.Combine(root, ".ai-context", "packs", "backend-docs.pack.yaml")));
        Assert.Contains("08-code-snippets.md", await File.ReadAllTextAsync(Path.Combine(root, ".ai-context", "exports", "backend-docs-compact.yaml")));
        Assert.DoesNotContain("GeneratedBy: SourceWorker", await File.ReadAllTextAsync(Path.Combine(output, "README.md")));
        Assert.Contains("SourceWorker:Generated", await File.ReadAllTextAsync(Path.Combine(output, "README.md")));
    }

    [Fact]
    public async Task Context_browser_hides_technical_and_export_files_by_default()
    {
        var root = await CreateSampleProject();
        var project = new Project(Guid.NewGuid(), "Sample", root, DateTimeOffset.UtcNow);
        var scanner = new BasicProjectScanner(new EmptyGitInspector());
        var scan = await scanner.ScanAsync(project, CancellationToken.None);
        var artifactRepository = new MemoryGeneratedArtifactRepository();

        await new MarkdownContextWriter(artifacts: artifactRepository, packWriter: new ContextPackService(artifactRepository)).WriteBasicContextAsync(scan, CancellationToken.None);
        await new BasicRecipeRunner(scanner, new MarkdownContextWriter(), new EmptyInstructionRepository(), artifactRepository, exportProfiles: new ExportProfileService(artifactRepository), packWriter: new ContextPackService(artifactRepository)).RunBackendDocsCompactAsync(scan, CancellationToken.None);
        var browser = new ContextBrowserService(new FileRecipeRepository(), new EmptyFlowRepository(), new EmptyRouteRepository(), artifactRepository);

        var userFiles = await browser.ListFilesAsync(project, showTechnical: false, showExports: false, CancellationToken.None);
        var technicalFiles = await browser.ListFilesAsync(project, showTechnical: true, showExports: true, CancellationToken.None);

        Assert.Contains(userFiles, f => f.RelativePath == ".ai-context/agent-handoff.md");
        Assert.DoesNotContain(userFiles, f => f.RelativePath == ".ai-context/index.yaml");
        Assert.DoesNotContain(userFiles, f => f.RelativePath.StartsWith("instructions/Result/Backend/", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(technicalFiles, f => f.RelativePath == ".ai-context/index.yaml");
        Assert.Contains(technicalFiles, f => f.RelativePath.StartsWith("instructions/Result/Backend/", StringComparison.OrdinalIgnoreCase));
    }

    private static async Task<string> CreateSampleProject()
    {
        var root = Path.Combine(Path.GetTempPath(), "sourceworker-scanner-" + Guid.NewGuid());
        Directory.CreateDirectory(Path.Combine(root, "src", "services"));
        await File.WriteAllTextAsync(Path.Combine(root, "Sample.sln"), "");
        await File.WriteAllTextAsync(Path.Combine(root, "src", "services", "Sample.Api.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk.Web\"><ItemGroup><ProjectReference Include=\"..\\Sample.Core\\Sample.Core.csproj\" /></ItemGroup></Project>");
        await File.WriteAllTextAsync(Path.Combine(root, "src", "services", "Program.cs"), "// REVIEW: sample");
        await File.WriteAllTextAsync(Path.Combine(root, "README.md"), "# Sample");
        await File.WriteAllTextAsync(Path.Combine(root, "appsettings.json"), "{}");
        return root;
    }

    private sealed class EmptyGitInspector : IGitInspector
    {
        public Task<GitSnapshot> InspectAsync(Project project, CancellationToken cancellationToken)
            => Task.FromResult(new GitSnapshot(Guid.NewGuid(), project.Id, null, null, false, DateTimeOffset.UtcNow));
    }

    private sealed class EmptyInstructionRepository : IProjectInstructionRepository
    {
        public Task<ProjectInstructionDocument?> GetLatestAsync(Guid projectId, CancellationToken cancellationToken) => Task.FromResult<ProjectInstructionDocument?>(null);
        public Task<ProjectInstructionDocument> SaveAsync(ProjectInstructionDocument document, CancellationToken cancellationToken) => Task.FromResult(document);
    }

    private sealed class MemoryGeneratedArtifactRepository : IGeneratedArtifactRepository
    {
        private readonly List<GeneratedArtifact> artifacts = [];
        public Task<GeneratedArtifact> UpsertAsync(GeneratedArtifact artifact, CancellationToken cancellationToken)
        {
            artifacts.RemoveAll(a => a.ProjectId == artifact.ProjectId && a.RelativePath.Equals(artifact.RelativePath, StringComparison.OrdinalIgnoreCase));
            artifacts.Add(artifact);
            return Task.FromResult(artifact);
        }

        public Task<IReadOnlyList<GeneratedArtifact>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<GeneratedArtifact>>(artifacts.Where(a => a.ProjectId == projectId).ToArray());

        public Task<GeneratedArtifact?> GetByPathAsync(Guid projectId, string relativePath, CancellationToken cancellationToken)
            => Task.FromResult(artifacts.FirstOrDefault(a => a.ProjectId == projectId && a.RelativePath.Equals(relativePath, StringComparison.OrdinalIgnoreCase)));
    }

    private sealed class EmptyFlowRepository : ICodeFlowRepository
    {
        public Task SaveAsync(Guid projectId, Guid scanRunId, IReadOnlyList<CodeFlow> flows, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<IReadOnlyList<CodeFlow>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<CodeFlow>>([]);
    }

    private sealed class EmptyRouteRepository : IRouteInfoRepository
    {
        public Task SaveAsync(Guid projectId, Guid scanRunId, IReadOnlyList<RouteInfo> routes, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<IReadOnlyList<RouteInfo>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<RouteInfo>>([]);
    }

    private sealed class SymbolRepo(IReadOnlyList<CodeSymbol> items) : ICodeSymbolRepository
    {
        public Task SaveAsync(Guid projectId, Guid scanRunId, IReadOnlyList<CodeSymbol> symbols, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<IReadOnlyList<CodeSymbol>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken) => Task.FromResult(items);
    }

    private sealed class FlowRepo(IReadOnlyList<CodeFlow> items) : ICodeFlowRepository
    {
        public Task SaveAsync(Guid projectId, Guid scanRunId, IReadOnlyList<CodeFlow> flows, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<IReadOnlyList<CodeFlow>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken) => Task.FromResult(items);
    }

    private sealed class RouteRepo(IReadOnlyList<RouteInfo> items) : IRouteInfoRepository
    {
        public Task SaveAsync(Guid projectId, Guid scanRunId, IReadOnlyList<RouteInfo> routes, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<IReadOnlyList<RouteInfo>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken) => Task.FromResult(items);
    }

    private sealed class ServiceRepo(IReadOnlyList<ServiceRegistration> items) : IServiceRegistrationRepository
    {
        public Task SaveAsync(Guid projectId, Guid scanRunId, IReadOnlyList<ServiceRegistration> registrations, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<IReadOnlyList<ServiceRegistration>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken) => Task.FromResult(items);
    }

    private sealed class ConfigRepo(IReadOnlyList<ConfigurationUsage> items) : IConfigurationUsageRepository
    {
        public Task SaveAsync(Guid projectId, Guid scanRunId, IReadOnlyList<ConfigurationUsage> usages, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<IReadOnlyList<ConfigurationUsage>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken) => Task.FromResult(items);
    }

    private sealed class TestRepo(IReadOnlyList<TestInfo> items) : ITestInfoRepository
    {
        public Task SaveAsync(Guid projectId, Guid scanRunId, IReadOnlyList<TestInfo> tests, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<IReadOnlyList<TestInfo>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken) => Task.FromResult(items);
    }
}
