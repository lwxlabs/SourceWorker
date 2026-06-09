using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using SourceWorker.Domain;

namespace SourceWorker.Application;

public sealed partial class ProjectTaskService(IProjectRepository projects) : IProjectTaskService
{
    private const int MaxFileSizeBytes = 1_000_000;
    private static readonly HashSet<string> SkippedDirectories = new(StringComparer.OrdinalIgnoreCase) { ".git", ".vs", ".idea", ".ai-context", "bin", "obj", "node_modules", "dist", "build" };
    private static readonly string[] TextExtensions = [".cs", ".razor", ".cshtml", ".js", ".jsx", ".ts", ".tsx", ".vue", ".html", ".css", ".scss", ".json", ".xml", ".yaml", ".yml", ".md", ".txt", ".config", ".props", ".targets", ".ps1", ".sh", ".sql"];
    private static readonly IReadOnlyDictionary<string, string> GroupAliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["FIXIT"] = "FIXME", ["FIX"] = "FIXME", ["HOTFIX"] = "FIXME",
        ["README"] = "NOTE", ["DOCS"] = "NOTE", ["INFO"] = "NOTE",
        ["PERF"] = "OPTIMIZE",
        ["MODIFIED"] = "CHANGED", ["REFACTOR"] = "CHANGED",
        ["OBSOLETE"] = "DEPRECATED",
        ["REVERT"] = "UNDONE", ["NOCOMMIT"] = "UNDONE", ["NOSHIP"] = "UNDONE",
        ["WAIT"] = "FUTURE",
        ["BREAKING"] = "BLOCKED",
        ["WIP"] = "TODO", ["TEMP"] = "TODO",
        ["QUESTION"] = "REVIEW",
        ["SECTION"] = "LEGACY",
        ["EXP"] = "EXPERIMENTAL", ["POC"] = "EXPERIMENTAL"
    };

    private static readonly IReadOnlyDictionary<string, string> Colors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["TODO"] = "#37B24D", ["FIXME"] = "#E5C100", ["BUG"] = "#E03131", ["NOTE"] = "#4DABF7",
        ["OPTIMIZE"] = "#FD7E14", ["HACK"] = "#845EF7", ["REVIEW"] = "#22B8CF", ["DEPRECATED"] = "#868E96",
        ["UNDONE"] = "#F08C00", ["CHANGED"] = "#E64980", ["FUTURE"] = "#20C997", ["LEGACY"] = "#FA5252",
        ["TEST"] = "#2DB8A0", ["DEBUG"] = "#1FB6FF", ["CLEANUP"] = "#20C997", ["SECURITY"] = "#FA5252",
        ["BLOCKED"] = "#A61E4D", ["MIGRATE"] = "#9775FA", ["IDEA"] = "#FAB005", ["EXPERIMENTAL"] = "#2DB8A0"
    };

    public async Task<ProjectTaskReport> ScanAsync(Guid projectId, ProjectTaskScanRequest request, CancellationToken cancellationToken)
    {
        var project = await GetProject(projectId, cancellationToken);
        var tags = ParseList(request.Tags, DefaultTags()).Select(t => request.UppercaseTags ? t.ToUpperInvariant() : t).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var prefixes = ParseList(request.Prefixes, DefaultPrefixes()).ToArray();
        var matcher = BuildMatcher(tags, prefixes, request.CaseSensitive, request.RequireSeparator);
        var items = new List<ProjectTaskItem>();
        var filesScanned = 0;

        foreach (var file in EnumerateFiles(project.RootPath, cancellationToken))
        {
            var info = new FileInfo(file);
            if (!ShouldRead(info)) continue;
            filesScanned++;
            var relative = NormalizeRelative(project.RootPath, file);
            var lines = await File.ReadAllLinesAsync(file, cancellationToken);
            for (var i = 0; i < lines.Length; i++)
            {
                foreach (Match match in matcher.Matches(lines[i]))
                {
                    if (!match.Success) continue;
                    var tag = match.Groups["tag"].Value;
                    var normalizedTag = tag.ToUpperInvariant();
                    var main = GroupAliases.TryGetValue(normalizedTag, out var group) ? group : normalizedTag;
                    var text = match.Groups["text"].Success ? match.Groups["text"].Value.Trim() : "";
                    var color = Colors.GetValueOrDefault(main, "#8F8F8F");
                    items.Add(new ProjectTaskItem(
                        StableId(relative, i + 1, match.Index, lines[i]),
                        tag,
                        main,
                        relative,
                        Path.GetFileName(relative),
                        i + 1,
                        match.Index + 1,
                        text,
                        lines[i].Trim(),
                        color));
                }
            }
        }

        var groups = items
            .GroupBy(i => i.MainTag, StringComparer.OrdinalIgnoreCase)
            .Select(g => new ProjectTaskGroup(g.Key, Colors.GetValueOrDefault(g.Key, "#8F8F8F"), g.Count(), g.OrderBy(i => i.RelativePath).ThenBy(i => i.LineNumber).ToArray()))
            .OrderByDescending(g => g.Count)
            .ThenBy(g => g.Name)
            .ToArray();

        return new ProjectTaskReport(projectId, DateTimeOffset.UtcNow, filesScanned, items.Count, groups, request);
    }

    public async Task<ProjectTaskFileContent> ReadFileAsync(Guid projectId, string relativePath, int? focusLine, CancellationToken cancellationToken)
    {
        var project = await GetProject(projectId, cancellationToken);
        var path = ResolveInsideProject(project, relativePath);
        if (!File.Exists(path)) throw new FileNotFoundException("Source file was not found.", path);
        var text = await File.ReadAllTextAsync(path, cancellationToken);
        var markdown = BuildFileMarkdown(relativePath, text, focusLine);
        return new ProjectTaskFileContent(relativePath, Path.GetFileName(relativePath), markdown, text, focusLine);
    }

    public async Task<ProjectTaskDeleteResult> DeleteAsync(Guid projectId, ProjectTaskDeleteRequest request, CancellationToken cancellationToken)
    {
        var project = await GetProject(projectId, cancellationToken);
        var path = ResolveInsideProject(project, request.RelativePath);
        if (!File.Exists(path)) throw new FileNotFoundException("Source file was not found.", path);
        var lines = (await File.ReadAllLinesAsync(path, cancellationToken)).ToList();
        if (request.LineNumber <= 0 || request.LineNumber > lines.Count) return new ProjectTaskDeleteResult(false, request.RelativePath, request.LineNumber, "The task line no longer exists.", null);

        var line = lines[request.LineNumber - 1];
        var match = FindTaskOnLine(line, request.Tag);
        if (!match.Success) return new ProjectTaskDeleteResult(false, request.RelativePath, request.LineNumber, "The task was not found on that line anymore.", BuildFileMarkdown(request.RelativePath, string.Join(Environment.NewLine, lines), request.LineNumber));

        if (string.IsNullOrWhiteSpace(line[..match.Index]))
        {
            lines.RemoveAt(request.LineNumber - 1);
        }
        else
        {
            lines[request.LineNumber - 1] = line[..match.Index].TrimEnd();
        }

        var updated = string.Join(Environment.NewLine, lines);
        await File.WriteAllTextAsync(path, updated, cancellationToken);
        return new ProjectTaskDeleteResult(true, request.RelativePath, request.LineNumber, null, BuildFileMarkdown(request.RelativePath, updated, Math.Min(request.LineNumber, lines.Count)));
    }

    public async Task<bool> OpenPathAsync(Guid projectId, ProjectTaskOpenRequest request, CancellationToken cancellationToken)
    {
        var project = await GetProject(projectId, cancellationToken);
        var path = ResolveInsideProject(project, request.RelativePath);
        var target = request.Folder ? Path.GetDirectoryName(path) ?? project.RootPath : path;
        if (!File.Exists(path) && !Directory.Exists(path)) return false;
        await Task.Run(() => Process.Start(new ProcessStartInfo(target) { UseShellExecute = true }), cancellationToken);
        return true;
    }

    private async Task<Project> GetProject(Guid projectId, CancellationToken cancellationToken)
        => await projects.GetAsync(projectId, cancellationToken) ?? throw new InvalidOperationException("Project was not found.");

    private IEnumerable<string> EnumerateFiles(string root, CancellationToken cancellationToken)
    {
        var ignored = LoadGitIgnore(root).Concat(AiIgnoreRules.LoadFileIgnorePatterns(root)).ToArray();
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.TryPop(out var directory))
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var childDirectory in Directory.EnumerateDirectories(directory))
            {
                var name = Path.GetFileName(childDirectory);
                var relative = NormalizeRelative(root, childDirectory);
                if (!SkippedDirectories.Contains(name) && !AiIgnoreRules.IsFileIgnored(relative, true, ignored)) pending.Push(childDirectory);
            }

            foreach (var file in Directory.EnumerateFiles(directory))
            {
                var relative = NormalizeRelative(root, file);
                if (!AiIgnoreRules.IsFileIgnored(relative, false, ignored)) yield return file;
            }
        }
    }

    private static Regex BuildMatcher(IReadOnlyList<string> tags, IReadOnlyList<string> prefixes, bool caseSensitive, bool requireSeparator)
    {
        var tagPattern = string.Join("|", tags.Select(Regex.Escape));
        var prefixPattern = string.Join("|", prefixes.OrderByDescending(p => p.Length).Select(Regex.Escape));
        var separator = requireSeparator ? @"(?=\s*(?:=>|->|:|-))\s*(?:=>|->|:|-)\s*" : @"(?:\s*(?:=>|->|:|-)\s*)?";
        var pattern = $@"(?:(?:{prefixPattern})\s*|^\s*)(?<tag>{tagPattern})\b{separator}(?<text>.*)$";
        var options = RegexOptions.CultureInvariant | RegexOptions.Compiled | (caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase);
        return new Regex(pattern, options, TimeSpan.FromMilliseconds(100));
    }

    private static Match FindTaskOnLine(string line, string tag)
    {
        var matcher = BuildMatcher([tag], DefaultPrefixes(), false, false);
        return matcher.Match(line);
    }

    private static string BuildFileMarkdown(string relativePath, string text, int? focusLine)
    {
        var language = LanguageFromExtension(Path.GetExtension(relativePath));
        var lines = text.Replace("\r\n", "\n").Split('\n');
        var builder = new StringBuilder();
        builder.AppendLine($"# {relativePath}");
        if (focusLine is not null) builder.AppendLine($"Focus line: {focusLine}");
        builder.AppendLine();
        builder.AppendLine($"```{language}");
        for (var i = 0; i < lines.Length; i++)
        {
            var marker = focusLine == i + 1 ? ">> " : "   ";
            builder.AppendLine($"{marker}{(i + 1).ToString().PadLeft(4)} | {lines[i]}");
        }
        builder.AppendLine("```");
        return builder.ToString();
    }

    private static bool ShouldRead(FileInfo file)
        => file.Length <= MaxFileSizeBytes && (TextExtensions.Contains(file.Extension, StringComparer.OrdinalIgnoreCase) || string.IsNullOrEmpty(file.Extension));

    private static string ResolveInsideProject(Project project, string relativePath)
    {
        var normalized = relativePath.Replace('\\', '/').TrimStart('/');
        var full = Path.GetFullPath(Path.Combine(project.RootPath, normalized));
        var root = Path.GetFullPath(project.RootPath);
        if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Path is outside the project root.");
        return full;
    }

    private static IReadOnlyList<string> LoadGitIgnore(string root)
    {
        var path = Path.Combine(root, ".gitignore");
        return File.Exists(path)
            ? File.ReadAllLines(path).Select(line => line.Trim()).Where(line => line.Length > 0 && !line.StartsWith('#')).ToArray()
            : [];
    }

    private static IReadOnlyList<string> ParseList(string? value, IReadOnlyList<string> fallback)
        => string.IsNullOrWhiteSpace(value)
            ? fallback
            : value.Split([',', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Where(v => !string.IsNullOrWhiteSpace(v)).ToArray();

    private static string StableId(string relativePath, int line, int column, string text)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{relativePath}:{line}:{column}:{text}"));
        return Convert.ToHexString(bytes[..8]).ToLowerInvariant();
    }

    private static string NormalizeRelative(string root, string path) => Path.GetRelativePath(root, path).Replace('\\', '/');

    private static string LanguageFromExtension(string extension) => extension.ToLowerInvariant() switch
    {
        ".cs" or ".razor" or ".cshtml" => "csharp",
        ".js" or ".jsx" => "javascript",
        ".ts" or ".tsx" => "typescript",
        ".json" => "json",
        ".xml" => "xml",
        ".yaml" or ".yml" => "yaml",
        ".md" => "markdown",
        ".css" or ".scss" => "css",
        ".html" => "html",
        ".sql" => "sql",
        ".ps1" => "powershell",
        ".sh" => "bash",
        _ => ""
    };

    private static IReadOnlyList<string> DefaultPrefixes() => ["//", "#", "<!--", ";", "/*", "{#"];

    private static IReadOnlyList<string> DefaultTags() =>
    [
        "TODO", "FIXME", "FIXIT", "FIX", "HOTFIX", "NOTE", "INFO", "README", "DOCS", "BUG", "OPTIMIZE", "PERF", "HACK",
        "REVIEW", "QUESTION", "FUTURE", "WAIT", "LEGACY", "CHANGED", "MODIFIED", "REFACTOR", "DEPRECATED", "OBSOLETE",
        "UNDONE", "REVERT", "NOCOMMIT", "NOSHIP", "TEST", "DEBUG", "TEMP", "WIP", "CLEANUP", "SECURITY", "BLOCKED",
        "BREAKING", "MIGRATE", "IDEA", "EXPERIMENTAL", "EXP", "POC"
    ];
}
