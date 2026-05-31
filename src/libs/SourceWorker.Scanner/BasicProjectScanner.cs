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
        var ignored = LoadGitIgnore(project.RootPath);

        foreach (var file in EnumerateFiles(project.RootPath, ignored, cancellationToken))
        {
            var relative = Path.GetRelativePath(project.RootPath, file).Replace('\\', '/');
            var info = new FileInfo(file);
            var kind = ClassifyFile(relative);
            files.Add(new IndexedFile(Guid.NewGuid(), project.Id, scanId, relative, info.Extension, info.Length, kind));
            DetectByPath(relative, technologies);

            if (!ShouldReadText(info)) continue;
            var lines = await File.ReadAllLinesAsync(file, cancellationToken);
            DetectByContent(relative, lines, technologies);
            todos.AddRange(FindTodos(project.Id, scanId, relative, lines));
        }

        var run = new ScanRun(scanId, project.Id, started, DateTimeOffset.UtcNow, ScanRunStatus.Completed, files.Count, todos.Count);
        var detected = technologies.Select(pair => new DetectedTechnology(Guid.NewGuid(), project.Id, scanId, pair.Key, pair.Value)).ToArray();
        var git = await gitInspector.InspectAsync(project, cancellationToken);
        return new ProjectScanResult(project, run, files, todos, detected, git);
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
                var name = Path.GetFileName(childDirectory);
                var relative = Path.GetRelativePath(root, childDirectory).Replace('\\', '/');
                if (!SkippedDirectories.Contains(name) && !IsIgnored(relative, true, gitIgnore)) pending.Push(childDirectory);
            }

            foreach (var file in Directory.EnumerateFiles(directory))
            {
                var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
                if (!IsIgnored(relative, false, gitIgnore)) yield return file;
            }
        }
    }

    private static IReadOnlyList<string> LoadGitIgnore(string root)
    {
        var path = Path.Combine(root, ".gitignore");
        return File.Exists(path)
            ? File.ReadAllLines(path).Select(line => line.Trim()).Where(line => line.Length > 0 && !line.StartsWith('#') && !line.StartsWith('!')).ToArray()
            : [];
    }

    private static bool IsIgnored(string relativePath, bool isDirectory, IReadOnlyList<string> patterns)
    {
        foreach (var pattern in patterns)
        {
            var normalized = pattern.Trim('/').Replace('\\', '/');
            if (normalized.Length == 0) continue;
            if (pattern.EndsWith('/') && isDirectory && relativePath.Equals(normalized, StringComparison.OrdinalIgnoreCase)) return true;
            if (relativePath.Equals(normalized, StringComparison.OrdinalIgnoreCase) || relativePath.StartsWith(normalized + '/', StringComparison.OrdinalIgnoreCase) || Path.GetFileName(relativePath).Equals(normalized, StringComparison.OrdinalIgnoreCase)) return true;
            if (normalized.StartsWith("*.") && relativePath.EndsWith(normalized[1..], StringComparison.OrdinalIgnoreCase)) return true;
        }

        return false;
    }

    private static IndexedFileKind ClassifyFile(string relativePath)
    {
        var fileName = Path.GetFileName(relativePath);
        var extension = Path.GetExtension(relativePath);
        if (extension.Equals(".sln", StringComparison.OrdinalIgnoreCase) || extension.Equals(".slnx", StringComparison.OrdinalIgnoreCase)) return IndexedFileKind.Solution;
        if (extension.Equals(".csproj", StringComparison.OrdinalIgnoreCase)) return IndexedFileKind.Project;
        if (fileName.Equals("Dockerfile", StringComparison.OrdinalIgnoreCase) || fileName.Equals("docker-compose.yml", StringComparison.OrdinalIgnoreCase)) return IndexedFileKind.Docker;
        if (relativePath.StartsWith(".github/workflows/", StringComparison.OrdinalIgnoreCase)) return IndexedFileKind.Workflow;
        if (extension.Equals(".md", StringComparison.OrdinalIgnoreCase)) return IndexedFileKind.Markdown;
        return extension is ".cs" or ".json" or ".xml" or ".yaml" or ".yml" ? IndexedFileKind.Source : IndexedFileKind.Other;
    }

    private static bool ShouldReadText(FileInfo file)
        => file.Length <= 1_000_000 && file.Extension is ".cs" or ".csproj" or ".sln" or ".slnx" or ".md" or ".json" or ".xml" or ".yaml" or ".yml";

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

    private static void DetectByPath(string relativePath, IDictionary<TechnologyKind, string> technologies)
    {
        var fileName = Path.GetFileName(relativePath);
        if (relativePath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)) technologies.TryAdd(TechnologyKind.DotNet, relativePath);
        if (fileName.Equals("Dockerfile", StringComparison.OrdinalIgnoreCase) || fileName.Equals("docker-compose.yml", StringComparison.OrdinalIgnoreCase)) technologies.TryAdd(TechnologyKind.Docker, relativePath);
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

    [GeneratedRegex(@"\b(TODO|FIXME|REVIEW)\b[:\s]*(.*)", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex TodoRegex();
}
