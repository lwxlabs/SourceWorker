using System.Security.Cryptography;
using System.Text.RegularExpressions;
using SourceWorker.Application;
using SourceWorker.Domain;

namespace SourceWorker.Scanner;

public sealed partial class BasicProjectScanner(IGitInspector gitInspector) : IProjectScanner
{
    private static readonly HashSet<string> SkippedDirectories = new(StringComparer.OrdinalIgnoreCase) { ".git", ".vs", ".idea", ".ai-context", "bin", "obj", "node_modules", "dist", "build" };

    public async Task<ProjectScanResult> ScanAsync(Project project, CancellationToken cancellationToken)
    {
        var scanId = Guid.NewGuid();
        var files = new List<IndexedFile>();
        var todos = new List<TodoItem>();
        var technologies = new Dictionary<TechnologyKind, string>();
        var started = DateTimeOffset.UtcNow;
        var ignored = LoadGitIgnore(project.RootPath).Concat(AiIgnoreRules.LoadFileIgnorePatterns(project.RootPath)).ToArray();
        var previousHashes = LoadPreviousHashes(project.RootPath);

        foreach (var file in EnumerateFiles(project.RootPath, ignored, cancellationToken))
        {
            var relative = NormalizeRelative(project.RootPath, file);
            var info = new FileInfo(file);
            var sha256 = await ComputeSha256Async(file, cancellationToken);
            var kind = ClassifyFile(relative);
            var changeKind = previousHashes.TryGetValue(relative, out var previousHash)
                ? previousHash.Equals(sha256, StringComparison.OrdinalIgnoreCase) ? FileChangeKind.Unchanged : FileChangeKind.Changed
                : FileChangeKind.New;

            files.Add(new IndexedFile(Guid.NewGuid(), project.Id, scanId, file, relative, info.Extension, info.Length, info.LastWriteTimeUtc, sha256, kind, false, null, changeKind));
            DetectByPath(relative, technologies);

            if (!ShouldReadText(info)) continue;
            var lines = await File.ReadAllLinesAsync(file, cancellationToken);
            DetectByContent(relative, lines, technologies);
            if (!IsGeneratedContextPath(relative))
                todos.AddRange(FindTodos(project.Id, scanId, relative, lines));
        }

        var solutionFiles = files.Where(f => f.Kind == IndexedFileKind.Solution)
            .Select(f => new SolutionFile(System.IO.Path.GetFileNameWithoutExtension(f.RelativePath), f.RelativePath))
            .OrderBy(f => f.RelativePath)
            .ToArray();
        var projectFiles = await ReadProjectsAsync(files.Where(f => f.Kind == IndexedFileKind.Project), cancellationToken);
        var readmes = files.Where(f => IsReadmeOrUsage(f.RelativePath)).Select(f => f.RelativePath).Order().ToArray();
        var configs = files.Where(f => f.Kind == IndexedFileKind.Config).Select(f => f.RelativePath).Order().ToArray();
        var dockerFiles = files.Where(f => f.Kind == IndexedFileKind.Docker).Select(f => f.RelativePath).Order().ToArray();
        var entrypoints = files.Where(f => IsEntrypoint(f.RelativePath)).Select(f => f.RelativePath).Order().ToArray();
        var folderRoles = DetectFolderRoles(project.RootPath);
        var deletedFiles = previousHashes.Keys.Except(files.Select(f => f.RelativePath), StringComparer.OrdinalIgnoreCase).Order().ToArray();

        var changedCount = files.Count(f => f.ChangeKind is FileChangeKind.New or FileChangeKind.Changed or FileChangeKind.Deleted);
        var run = new ScanRun(scanId, project.Id, started, DateTimeOffset.UtcNow, ScanRunStatus.Completed, files.Count, todos.Count, files.Count, 0, changedCount, technologies.Count);
        var detected = technologies.Select(pair => new DetectedTechnology(Guid.NewGuid(), project.Id, scanId, pair.Key, pair.Value)).OrderBy(t => t.Kind).ToArray();
        var git = await gitInspector.InspectAsync(project, cancellationToken);
        var primarySolution = solutionFiles.FirstOrDefault();

        return new ProjectScanResult(
            project,
            run,
            files.OrderBy(f => f.RelativePath).ToArray(),
            todos.OrderBy(t => t.RelativePath).ThenBy(t => t.LineNumber).ToArray(),
            detected,
            git,
            primarySolution?.Name,
            primarySolution?.RelativePath,
            solutionFiles,
            projectFiles,
            readmes,
            configs,
            dockerFiles,
            folderRoles,
            entrypoints,
            deletedFiles);
    }

    private static IEnumerable<string> EnumerateFiles(string root, IReadOnlyList<string> gitIgnore, CancellationToken cancellationToken)
    {
        var pending = new Stack<string>();
        pending.Push(root);

        while (pending.TryPop(out var directory))
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var childDirectory in Directory.EnumerateDirectories(directory))
            {
                var name = System.IO.Path.GetFileName(childDirectory);
                var relative = NormalizeRelative(root, childDirectory);
                if (!SkippedDirectories.Contains(name) && !IsIgnored(relative, true, gitIgnore)) pending.Push(childDirectory);
            }

            foreach (var file in Directory.EnumerateFiles(directory))
            {
                var relative = NormalizeRelative(root, file);
                if (!IsIgnored(relative, false, gitIgnore)) yield return file;
            }
        }
    }

    private static IReadOnlyList<string> LoadGitIgnore(string root)
    {
        var path = System.IO.Path.Combine(root, ".gitignore");
        return File.Exists(path)
            ? File.ReadAllLines(path).Select(line => line.Trim()).Where(line => line.Length > 0 && !line.StartsWith('#')).ToArray()
            : [];
    }

    private static bool IsIgnored(string relativePath, bool isDirectory, IReadOnlyList<string> patterns)
        => AiIgnoreRules.IsFileIgnored(relativePath, isDirectory, patterns);

    private static IndexedFileKind ClassifyFile(string relativePath)
    {
        var fileName = System.IO.Path.GetFileName(relativePath);
        var extension = System.IO.Path.GetExtension(relativePath);
        if (extension.Equals(".sln", StringComparison.OrdinalIgnoreCase) || extension.Equals(".slnx", StringComparison.OrdinalIgnoreCase)) return IndexedFileKind.Solution;
        if (extension.Equals(".csproj", StringComparison.OrdinalIgnoreCase)) return IndexedFileKind.Project;
        if (fileName.Equals("Dockerfile", StringComparison.OrdinalIgnoreCase) || fileName.Equals("docker-compose.yml", StringComparison.OrdinalIgnoreCase) || fileName.Equals("compose.yml", StringComparison.OrdinalIgnoreCase)) return IndexedFileKind.Docker;
        if (relativePath.StartsWith(".github/workflows/", StringComparison.OrdinalIgnoreCase)) return IndexedFileKind.Workflow;
        if (extension.Equals(".md", StringComparison.OrdinalIgnoreCase)) return IndexedFileKind.Markdown;
        if (IsConfig(relativePath)) return IndexedFileKind.Config;
        return extension is ".cs" or ".json" or ".xml" or ".yaml" or ".yml" ? IndexedFileKind.Source : IndexedFileKind.Other;
    }

    private static bool ShouldReadText(FileInfo file)
        => file.Length <= 1_000_000 && file.Extension is ".cs" or ".csproj" or ".sln" or ".slnx" or ".md" or ".json" or ".xml" or ".yaml" or ".yml" or ".config";

    private static IEnumerable<TodoItem> FindTodos(Guid projectId, Guid scanRunId, string relativePath, string[] lines)
    {
        for (var i = 0; i < lines.Length; i++)
        {
            var match = TodoRegex().Match(lines[i]);
            if (!match.Success) continue;
            var kind = Enum.Parse<TodoKind>(match.Groups[1].Value, ignoreCase: true);
            yield return new TodoItem(Guid.NewGuid(), projectId, scanRunId, relativePath, i + 1, kind, lines[i].Trim());
        }
    }

    private static bool IsGeneratedContextPath(string relativePath)
        => relativePath.StartsWith(".ai-context/", StringComparison.OrdinalIgnoreCase) ||
           relativePath.StartsWith("instructions/Result/", StringComparison.OrdinalIgnoreCase);

    private static void DetectByPath(string relativePath, IDictionary<TechnologyKind, string> technologies)
    {
        var fileName = System.IO.Path.GetFileName(relativePath);
        if (relativePath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)) technologies.TryAdd(TechnologyKind.DotNet, relativePath);
        if (fileName.Equals("Dockerfile", StringComparison.OrdinalIgnoreCase) || fileName.Contains("docker-compose", StringComparison.OrdinalIgnoreCase) || fileName.Equals("compose.yml", StringComparison.OrdinalIgnoreCase)) technologies.TryAdd(TechnologyKind.Docker, relativePath);
        if (relativePath.StartsWith(".github/workflows/", StringComparison.OrdinalIgnoreCase)) technologies.TryAdd(TechnologyKind.GitHubActions, relativePath);
    }

    private static void DetectByContent(string relativePath, string[] lines, IDictionary<TechnologyKind, string> technologies)
    {
        var content = string.Join('\n', lines);
        if (!relativePath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)) return;
        if (content.Contains("Microsoft.NET.Sdk.Web", StringComparison.OrdinalIgnoreCase)) technologies.TryAdd(TechnologyKind.AspNetCore, relativePath);
        if (content.Contains("Microsoft.NET.Sdk.Razor", StringComparison.OrdinalIgnoreCase) || content.Contains("Microsoft.AspNetCore.Components", StringComparison.OrdinalIgnoreCase)) technologies.TryAdd(TechnologyKind.Blazor, relativePath);
        if (content.Contains("Microsoft.Maui", StringComparison.OrdinalIgnoreCase) || content.Contains("UseMaui", StringComparison.OrdinalIgnoreCase)) technologies.TryAdd(TechnologyKind.Maui, relativePath);
    }

    private static async Task<IReadOnlyList<ProjectFile>> ReadProjectsAsync(IEnumerable<IndexedFile> projectFiles, CancellationToken cancellationToken)
    {
        var projects = new List<ProjectFile>();
        foreach (var file in projectFiles.OrderBy(f => f.RelativePath))
        {
            var text = await File.ReadAllTextAsync(file.Path, cancellationToken);
            var sdk = ProjectSdkRegex().Match(text).Groups["sdk"].Value;
            var refs = ProjectReferenceRegex().Matches(text)
                .Select(match => match.Groups["path"].Value.Replace('\\', '/'))
                .Order()
                .ToArray();
            var name = System.IO.Path.GetFileNameWithoutExtension(file.RelativePath);
            var isTest = name.EndsWith(".Tests", StringComparison.OrdinalIgnoreCase) ||
                file.RelativePath.Contains("/tests/", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("Microsoft.NET.Test.Sdk", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("xunit", StringComparison.OrdinalIgnoreCase);

            projects.Add(new ProjectFile(name, file.RelativePath, sdk, refs, isTest));
        }

        return projects;
    }

    private static IReadOnlyList<FolderRole> DetectFolderRoles(string root)
    {
        var roles = new List<FolderRole>();
        AddIfExists("src/core", "core", "Domain, application, and contracts");
        AddIfExists("src/infrastructure", "infrastructure", "Adapters for storage, file system, Git, markdown, and external boundaries");
        AddIfExists("src/libs", "libs", "Reusable local engine libraries");
        AddIfExists("src/services", "services", "Runnable hosts such as CLI, Local API, and workers");
        AddIfExists("tests", "tests", "Automated test projects");
        AddIfExists("migrations", "migrations", "Database schema or data migrations");
        AddIfExists("ui", "ui", "User interface assets or shell");
        return roles;

        void AddIfExists(string relativePath, string role, string description)
        {
            if (Directory.Exists(System.IO.Path.Combine(root, relativePath))) roles.Add(new FolderRole(relativePath, role, description));
        }
    }

    private static bool IsReadmeOrUsage(string relativePath)
    {
        var name = System.IO.Path.GetFileName(relativePath);
        return name.StartsWith("README", StringComparison.OrdinalIgnoreCase) || name.StartsWith("USAGE", StringComparison.OrdinalIgnoreCase) || relativePath.StartsWith("docs/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsConfig(string relativePath)
    {
        var name = System.IO.Path.GetFileName(relativePath);
        return name.StartsWith("appsettings", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("nuget.config", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("global.json", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("Directory.Build.props", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("Directory.Packages.props", StringComparison.OrdinalIgnoreCase) ||
            name.EndsWith(".config", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsEntrypoint(string relativePath)
    {
        var name = System.IO.Path.GetFileName(relativePath);
        return name.Equals("Program.cs", StringComparison.OrdinalIgnoreCase) || name.Equals("Startup.cs", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var bytes = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static Dictionary<string, string> LoadPreviousHashes(string root)
    {
        var index = System.IO.Path.Combine(root, ".ai-context", "index.yaml");
        if (!File.Exists(index)) return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var hashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string? currentPath = null;
        foreach (var line in File.ReadLines(index))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("- path: ", StringComparison.Ordinal))
            {
                currentPath = trimmed["- path: ".Length..].Trim().Trim('"');
            }
            else if (currentPath is not null && trimmed.StartsWith("sha256: ", StringComparison.Ordinal))
            {
                hashes[currentPath] = trimmed["sha256: ".Length..].Trim().Trim('"');
                currentPath = null;
            }
        }

        return hashes;
    }

    private static string NormalizeRelative(string root, string path)
        => System.IO.Path.GetRelativePath(root, path).Replace('\\', '/');

    [GeneratedRegex(@"\b(TODO|FIXME|REVIEW)\b[:\s]*(.*)", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex TodoRegex();

    [GeneratedRegex("<Project\\s+Sdk=\"(?<sdk>[^\"]+)\"", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex ProjectSdkRegex();

    [GeneratedRegex("<ProjectReference\\s+Include=\"(?<path>[^\"]+)\"", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex ProjectReferenceRegex();
}
