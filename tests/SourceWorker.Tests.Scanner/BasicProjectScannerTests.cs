using SourceWorker.Application;
using SourceWorker.Domain;
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
        Assert.Contains(result.Technologies, tech => tech.Kind == TechnologyKind.DotNet);
        Assert.Contains(result.Technologies, tech => tech.Kind == TechnologyKind.AspNetCore);
        Assert.Single(result.Todos);
        Assert.DoesNotContain(result.Files, file => file.RelativePath.Contains("bin/", StringComparison.OrdinalIgnoreCase));
    }

    private sealed class EmptyGitInspector : IGitInspector
    {
        public Task<GitSnapshot> InspectAsync(Project project, CancellationToken cancellationToken)
            => Task.FromResult(new GitSnapshot(Guid.NewGuid(), project.Id, null, null, false, DateTimeOffset.UtcNow));
    }
}
