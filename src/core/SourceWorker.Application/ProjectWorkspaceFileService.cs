using SourceWorker.Domain;

namespace SourceWorker.Application;

public sealed class ProjectWorkspaceFileService(IProjectRepository projects) : IProjectWorkspaceFileService
{
    private const long MaxTextFileBytes = 5_000_000;
    private static readonly HashSet<string> SkippedFolders = new(StringComparer.OrdinalIgnoreCase) { ".git", "bin", "obj", "node_modules", "dist", "build", ".vs", ".idea" };

    public async Task<IReadOnlyList<ProjectWorkspaceFileNode>> GetTreeAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var project = await GetProject(projectId, cancellationToken);
        return Directory.EnumerateFileSystemEntries(project.RootPath)
            .OrderByDescending(Directory.Exists)
            .ThenBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .Select(path => BuildNode(project.RootPath, path, cancellationToken))
            .Where(node => node is not null)
            .Cast<ProjectWorkspaceFileNode>()
            .ToArray();
    }

    public async Task<ProjectWorkspaceFileContent> ReadFileAsync(Guid projectId, string path, CancellationToken cancellationToken)
    {
        var project = await GetProject(projectId, cancellationToken);
        var full = ResolveInsideProject(project.RootPath, path);
        if (!File.Exists(full)) throw new FileNotFoundException("File was not found.", full);
        var info = new FileInfo(full);
        if (info.Length > MaxTextFileBytes) throw new InvalidOperationException("File is too large to edit in the project file editor.");
        var content = await File.ReadAllTextAsync(full, cancellationToken);
        if (content.IndexOf('\0') >= 0) throw new InvalidOperationException("Binary files cannot be edited in the project file editor.");
        var editorPath = ToEditorPath(project.RootPath, full);
        return new ProjectWorkspaceFileContent(editorPath, Path.GetFileName(full), content, DetectLanguage(full));
    }

    public async Task SaveFileAsync(Guid projectId, ProjectWorkspaceSaveFileRequest request, CancellationToken cancellationToken)
    {
        var project = await GetProject(projectId, cancellationToken);
        var full = ResolveInsideProject(project.RootPath, request.Path);
        if (!File.Exists(full)) throw new FileNotFoundException("File was not found.", full);
        await File.WriteAllTextAsync(full, request.Content, cancellationToken);
    }

    public async Task<string> CreateFileAsync(Guid projectId, ProjectWorkspaceCreateFileRequest request, CancellationToken cancellationToken)
    {
        var project = await GetProject(projectId, cancellationToken);
        var folder = ResolveFolderInsideProject(project.RootPath, request.FolderPath);
        Directory.CreateDirectory(folder);
        var full = EnsureUniqueFile(Path.Combine(folder, SafeName(request.Name)));
        await File.WriteAllTextAsync(full, request.Content ?? "", cancellationToken);
        return ToEditorPath(project.RootPath, full);
    }

    public async Task<string> CreateFolderAsync(Guid projectId, ProjectWorkspaceCreateFolderRequest request, CancellationToken cancellationToken)
    {
        var project = await GetProject(projectId, cancellationToken);
        var parent = ResolveFolderInsideProject(project.RootPath, request.ParentPath);
        var full = EnsureUniqueFolder(Path.Combine(parent, SafeName(request.Name)));
        Directory.CreateDirectory(full);
        await Task.CompletedTask;
        return ToEditorPath(project.RootPath, full);
    }

    public async Task<string> RenameAsync(Guid projectId, ProjectWorkspaceRenameRequest request, CancellationToken cancellationToken)
    {
        var project = await GetProject(projectId, cancellationToken);
        var full = ResolveInsideProject(project.RootPath, request.Path);
        var parent = Path.GetDirectoryName(full) ?? project.RootPath;
        var target = Path.Combine(parent, SafeName(request.NewName));
        if (File.Exists(full))
        {
            if (File.Exists(target) || Directory.Exists(target)) target = EnsureUniqueFile(target);
            File.Move(full, target);
        }
        else if (Directory.Exists(full))
        {
            if (File.Exists(target) || Directory.Exists(target)) target = EnsureUniqueFolder(target);
            Directory.Move(full, target);
        }
        else
        {
            throw new FileNotFoundException("Path was not found.", full);
        }

        await Task.CompletedTask;
        return ToEditorPath(project.RootPath, target);
    }

    public async Task DeleteAsync(Guid projectId, string path, CancellationToken cancellationToken)
    {
        var project = await GetProject(projectId, cancellationToken);
        var full = ResolveInsideProject(project.RootPath, path);
        if (File.Exists(full)) File.Delete(full);
        else if (Directory.Exists(full)) Directory.Delete(full, recursive: true);
        await Task.CompletedTask;
    }

    public async Task<string> DuplicateFileAsync(Guid projectId, ProjectWorkspaceDuplicateFileRequest request, CancellationToken cancellationToken)
    {
        var project = await GetProject(projectId, cancellationToken);
        var full = ResolveInsideProject(project.RootPath, request.Path);
        if (!File.Exists(full)) throw new FileNotFoundException("File was not found.", full);
        var folder = Path.GetDirectoryName(full) ?? project.RootPath;
        var extension = Path.GetExtension(full);
        var name = Path.GetFileNameWithoutExtension(full);
        var copy = EnsureUniqueFile(Path.Combine(folder, $"{name}.copy{extension}"));
        File.Copy(full, copy);
        await Task.CompletedTask;
        return ToEditorPath(project.RootPath, copy);
    }

    public async Task<IReadOnlyList<string>> GetFilePathsAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var project = await GetProject(projectId, cancellationToken);
        return Directory.EnumerateFiles(project.RootPath, "*", SearchOption.AllDirectories)
            .Where(path => !IsSkippedPath(project.RootPath, path))
            .Select(path => ToEditorPath(project.RootPath, path))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private async Task<Project> GetProject(Guid projectId, CancellationToken cancellationToken)
        => await projects.GetAsync(projectId, cancellationToken) ?? throw new InvalidOperationException("Project was not found.");

    private static ProjectWorkspaceFileNode? BuildNode(string root, string fullPath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var name = Path.GetFileName(fullPath);
        var isFolder = Directory.Exists(fullPath);
        if (isFolder && SkippedFolders.Contains(name)) return null;
        var editorPath = ToEditorPath(root, fullPath);
        var children = isFolder
            ? Directory.EnumerateFileSystemEntries(fullPath)
                .OrderByDescending(Directory.Exists)
                .ThenBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
                .Select(path => BuildNode(root, path, cancellationToken))
                .Where(node => node is not null)
                .Cast<ProjectWorkspaceFileNode>()
                .ToArray()
            : [];

        return new ProjectWorkspaceFileNode(Path.GetFullPath(fullPath), name, editorPath, isFolder, isFolder ? null : Path.GetExtension(fullPath), isFolder ? null : DetectLanguage(fullPath), children);
    }

    private static string ResolveInsideProject(string root, string editorPath)
    {
        var relative = NormalizeEditorPath(editorPath);
        var full = Path.GetFullPath(Path.Combine(root, relative));
        var normalizedRoot = Path.GetFullPath(root);
        if (!full.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Path is outside the project root.");
        return full;
    }

    private static string ResolveFolderInsideProject(string root, string editorPath)
    {
        var full = ResolveInsideProject(root, editorPath);
        if (File.Exists(full)) full = Path.GetDirectoryName(full) ?? root;
        return full;
    }

    private static bool IsSkippedPath(string root, string path)
    {
        var relative = Path.GetRelativePath(root, path).Replace('\\', '/');
        return relative.Split('/').Any(part => SkippedFolders.Contains(part));
    }

    private static string NormalizeEditorPath(string path)
        => string.IsNullOrWhiteSpace(path) || path == "/" ? "" : path.Replace('\\', '/').TrimStart('/').TrimEnd('/');

    private static string ToEditorPath(string root, string fullPath)
    {
        var relative = Path.GetRelativePath(root, fullPath).Replace('\\', '/');
        return relative == "." ? "/" : "/" + relative.Trim('/');
    }

    private static string SafeName(string name)
    {
        var trimmed = name.Trim();
        if (string.IsNullOrWhiteSpace(trimmed)) throw new InvalidOperationException("Name is required.");
        if (trimmed.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || trimmed.Contains('/') || trimmed.Contains('\\')) throw new InvalidOperationException("Name contains invalid path characters.");
        return trimmed;
    }

    private static string EnsureUniqueFile(string path)
    {
        if (!File.Exists(path) && !Directory.Exists(path)) return path;
        var folder = Path.GetDirectoryName(path) ?? "";
        var extension = Path.GetExtension(path);
        var name = Path.GetFileNameWithoutExtension(path);
        var i = 2;
        string candidate;
        do candidate = Path.Combine(folder, $"{name}-{i++}{extension}");
        while (File.Exists(candidate) || Directory.Exists(candidate));
        return candidate;
    }

    private static string EnsureUniqueFolder(string path)
    {
        if (!File.Exists(path) && !Directory.Exists(path)) return path;
        var parent = Path.GetDirectoryName(path) ?? "";
        var name = Path.GetFileName(path);
        var i = 2;
        string candidate;
        do candidate = Path.Combine(parent, $"{name}-{i++}");
        while (File.Exists(candidate) || Directory.Exists(candidate));
        return candidate;
    }

    private static string DetectLanguage(string path)
    {
        var name = Path.GetFileName(path);
        var extension = Path.GetExtension(path).ToLowerInvariant();
        if (name.Equals("Dockerfile", StringComparison.OrdinalIgnoreCase)) return "dockerfile";
        if (name.Equals(".gitignore", StringComparison.OrdinalIgnoreCase)) return "ignore";
        if (name.Equals(".env", StringComparison.OrdinalIgnoreCase)) return "env";
        return extension switch
        {
            ".cs" => "csharp",
            ".razor" => "razor",
            ".js" => "javascript",
            ".jsx" => "jsx",
            ".ts" => "typescript",
            ".tsx" => "tsx",
            ".json" => "json",
            ".css" => "css",
            ".scss" => "scss",
            ".html" => "html",
            ".md" => "markdown",
            ".xml" => "xml",
            ".yaml" or ".yml" => "yaml",
            ".ps1" => "powershell",
            ".sh" => "shell",
            ".sql" => "sql",
            _ => "plaintext"
        };
    }
}
