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
        Assert.True(File.Exists(Path.Combine(output, "README.md")));
        Assert.True(File.Exists(Path.Combine(output, "01-project-structure.md")));
        Assert.True(File.Exists(Path.Combine(output, "02-architecture.md")));
        Assert.True(File.Exists(Path.Combine(output, "03-important-files-and-flows.md")));
        Assert.True(File.Exists(Path.Combine(output, "04-configuration-and-secrets.md")));
        Assert.True(File.Exists(Path.Combine(output, "05-tasks-and-risks.md")));
        Assert.True(File.Exists(Path.Combine(output, "06-verification-checklist.md")));
        Assert.True(File.Exists(Path.Combine(external, "README.md")));
        Assert.True(File.Exists(Path.Combine(external, "06-verification-checklist.md")));
        Assert.True(File.Exists(Path.Combine(root, ".ai-context", "exports", "backend-docs-compact.yaml")));
        Assert.True(File.Exists(Path.Combine(root, ".ai-context", "packs", "backend-docs.pack.yaml")));
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
}
