using LwXLab.FileEditor.Models;
using LwXLab.FileEditor.Services;
using SourceWorker.ApiClient;

namespace SourceWorker.UI.Services;

public sealed class ProjectFileEditorContext
{
    public Guid? ProjectId { get; private set; }

    public void SetProject(Guid projectId) => ProjectId = projectId;

    public Guid RequireProjectId()
        => ProjectId ?? throw new InvalidOperationException("No project is selected for the file editor.");
}

public sealed class SourceWorkerFileEditorStorage(ISourceWorkerApiClient api, ProjectFileEditorContext context) : ILwFileEditorStorage
{
    public async Task<IReadOnlyList<LwFileEditorNode>> GetTreeAsync(CancellationToken cancellationToken = default)
        => (await api.GetProjectFileTreeAsync(context.RequireProjectId(), cancellationToken)).Select(MapNode).ToArray();

    public async Task<LwFileEditorFileContent> ReadFileAsync(string path, CancellationToken cancellationToken = default)
    {
        var file = await api.GetProjectFileContentAsync(context.RequireProjectId(), path, cancellationToken);
        return new LwFileEditorFileContent
        {
            Path = file.Path,
            Name = file.Name,
            Content = file.Content,
            Language = file.Language,
            IsReadonly = file.IsReadonly
        };
    }

    public Task SaveFileAsync(string path, string content, CancellationToken cancellationToken = default)
        => api.SaveProjectFileContentAsync(context.RequireProjectId(), new ProjectWorkspaceSaveFileRequestDto(path, content), cancellationToken);

    public Task<string> CreateFileAsync(string folderPath, string name, string? content = null, CancellationToken cancellationToken = default)
        => api.CreateProjectFileAsync(context.RequireProjectId(), new ProjectWorkspaceCreateFileRequestDto(folderPath, name, content), cancellationToken);

    public Task<string> CreateFolderAsync(string parentPath, string name, CancellationToken cancellationToken = default)
        => api.CreateProjectFolderAsync(context.RequireProjectId(), new ProjectWorkspaceCreateFolderRequestDto(parentPath, name), cancellationToken);

    public Task<string> RenameAsync(string path, string newName, CancellationToken cancellationToken = default)
        => api.RenameProjectFilePathAsync(context.RequireProjectId(), new ProjectWorkspaceRenameRequestDto(path, newName), cancellationToken);

    public Task DeleteAsync(string path, CancellationToken cancellationToken = default)
        => api.DeleteProjectFilePathAsync(context.RequireProjectId(), path, cancellationToken);

    public Task<string> DuplicateFileAsync(string path, CancellationToken cancellationToken = default)
        => api.DuplicateProjectFileAsync(context.RequireProjectId(), new ProjectWorkspaceDuplicateFileRequestDto(path), cancellationToken);

    public Task<IReadOnlyList<string>> GetFilePathsAsync(CancellationToken cancellationToken = default)
        => api.GetProjectFilePathsAsync(context.RequireProjectId(), cancellationToken);

    private static LwFileEditorNode MapNode(ProjectWorkspaceFileNodeDto node) => new()
    {
        Id = node.Id,
        Name = node.Name,
        Path = node.Path,
        Type = node.IsFolder ? LwFileEditorNodeType.Folder : LwFileEditorNodeType.File,
        Extension = node.Extension,
        Language = node.Language,
        IsExpanded = false,
        Children = node.Children.Select(MapNode).ToList()
    };
}
