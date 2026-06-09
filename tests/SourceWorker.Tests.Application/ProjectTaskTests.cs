using SourceWorker.Application;
using SourceWorker.Domain;

namespace SourceWorker.Tests.Application;

public sealed class ProjectTaskTests
{
    [Fact]
    public async Task Task_scan_respects_ai_ignore_and_groups_aliases()
    {
        using var workspace = new TempWorkspace();
        await workspace.WriteAsync("src/Program.cs", "var value = 1; // TODO: keep this");
        await workspace.WriteAsync("src/Fix.cs", "// FIX: grouped as fixme");
        await workspace.WriteAsync("generated-data.json", "{ \"note\": \"TODO: ignored\" }");
        await workspace.WriteAsync(".ai-context/ai-ignore.md", """
            enabled: true
            generated-*.json
            """);
        var service = workspace.CreateService();

        var report = await service.ScanAsync(workspace.Project.Id, new ProjectTaskScanRequest(), CancellationToken.None);

        Assert.Equal(2, report.TaskCount);
        Assert.Contains(report.Groups, group => group.Name == "TODO");
        Assert.Contains(report.Groups, group => group.Name == "FIXME");
        Assert.DoesNotContain(report.Groups.SelectMany(g => g.Items), item => item.RelativePath == "generated-data.json");
    }

    [Fact]
    public async Task Delete_inline_task_keeps_code_before_marker()
    {
        using var workspace = new TempWorkspace();
        await workspace.WriteAsync("src/Program.cs", "var value = 1; // TODO: remove this");
        var service = workspace.CreateService();

        var result = await service.DeleteAsync(workspace.Project.Id, new ProjectTaskDeleteRequest("src/Program.cs", 1, "TODO", "var value = 1; // TODO: remove this"), CancellationToken.None);

        Assert.True(result.Deleted);
        Assert.Equal("var value = 1;", await File.ReadAllTextAsync(Path.Combine(workspace.Root, "src", "Program.cs")));
    }

    private sealed class TempWorkspace : IDisposable
    {
        private readonly ProjectRepo projects = new();

        public string Root { get; } = Path.Combine(Path.GetTempPath(), "sourceworker-tasks-" + Guid.NewGuid());
        public Project Project { get; }

        public TempWorkspace()
        {
            Directory.CreateDirectory(Root);
            Project = new Project(Guid.NewGuid(), "Tasks", Root, DateTimeOffset.UtcNow);
            projects.Project = Project;
        }

        public async Task WriteAsync(string relativePath, string content)
        {
            var path = Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(path, content);
        }

        public ProjectTaskService CreateService() => new(projects);

        public void Dispose()
        {
            if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true);
        }
    }

    private sealed class ProjectRepo : IProjectRepository
    {
        public Project? Project { get; set; }

        public Task<Project> AddAsync(Project project, CancellationToken cancellationToken) => Task.FromResult(project);
        public Task<Project?> GetAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(Project?.Id == id ? Project : null);
        public Task<Project?> GetByPathAsync(string rootPath, CancellationToken cancellationToken) => Task.FromResult<Project?>(null);
        public Task<IReadOnlyList<Project>> ListAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Project>>(Project is null ? [] : [Project]);
        public Task<Project> UpdateAsync(Project project, CancellationToken cancellationToken) => Task.FromResult(project);
        public Task DeleteAsync(Guid id, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
