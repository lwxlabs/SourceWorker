using SourceWorker.Application;
using SourceWorker.Domain;

namespace SourceWorker.Tests.Application;

public sealed class ProjectWorkspaceFileTests
{
    [Fact]
    public async Task File_workspace_can_read_save_create_and_rename_inside_project()
    {
        using var workspace = new TempWorkspace();
        await workspace.WriteAsync("README.md", "# Old");
        var service = workspace.CreateService();

        var read = await service.ReadFileAsync(workspace.Project.Id, "/README.md", CancellationToken.None);
        await service.SaveFileAsync(workspace.Project.Id, new ProjectWorkspaceSaveFileRequest("/README.md", "# New"), CancellationToken.None);
        var created = await service.CreateFileAsync(workspace.Project.Id, new ProjectWorkspaceCreateFileRequest("/", "notes.md", "hello"), CancellationToken.None);
        var renamed = await service.RenameAsync(workspace.Project.Id, new ProjectWorkspaceRenameRequest(created, "notes-renamed.md"), CancellationToken.None);

        Assert.Equal("# Old", read.Content);
        Assert.Equal("# New", await File.ReadAllTextAsync(Path.Combine(workspace.Root, "README.md")));
        Assert.Equal("/notes-renamed.md", renamed);
        Assert.True(File.Exists(Path.Combine(workspace.Root, "notes-renamed.md")));
    }

    [Fact]
    public async Task File_workspace_blocks_path_traversal()
    {
        using var workspace = new TempWorkspace();
        var service = workspace.CreateService();

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ReadFileAsync(workspace.Project.Id, "../outside.txt", CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateFileAsync(workspace.Project.Id, new ProjectWorkspaceCreateFileRequest("/", "../outside.txt", ""), CancellationToken.None));
    }

    private sealed class TempWorkspace : IDisposable
    {
        private readonly ProjectRepo projects = new();

        public string Root { get; } = Path.Combine(Path.GetTempPath(), "sourceworker-files-" + Guid.NewGuid());
        public Project Project { get; }

        public TempWorkspace()
        {
            Directory.CreateDirectory(Root);
            Project = new Project(Guid.NewGuid(), "Files", Root, DateTimeOffset.UtcNow);
            projects.Project = Project;
        }

        public async Task WriteAsync(string relativePath, string content)
        {
            var path = Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(path, content);
        }

        public ProjectWorkspaceFileService CreateService() => new(projects);

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
