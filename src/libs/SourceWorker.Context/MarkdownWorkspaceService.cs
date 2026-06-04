using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using SourceWorker.Application;
using SourceWorker.Domain;

namespace SourceWorker.Context;

public sealed class MarkdownWorkspaceService(IGeneratedArtifactRepository artifacts, IMermaidMarkdownValidator mermaid)
    : IMarkdownWorkspaceService, IMarkdownFileReader, IMarkdownFileWriter, IMarkdownSavePolicyService
{
    private static readonly string[] BlockedFolders = [".git", "bin", "obj", ".vs", ".idea"];
    private static readonly string[] BlockedNames = [".env"];
    private static readonly string[] MarkdownExtensions = [".md", ".markdown"];

    public async Task<IReadOnlyList<MarkdownFileSummary>> ListFilesAsync(Project project, MarkdownWorkspaceScope? scope, bool includeTechnical, bool includeExternalMirror, bool includeUserMarkdown, CancellationToken cancellationToken)
    {
        var roots = Roots(project, includeExternalMirror, includeUserMarkdown);
        var artifactMap = (await artifacts.GetByProjectAsync(project.Id, cancellationToken))
            .GroupBy(a => Normalize(a.RelativePath), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(a => a.GeneratedUtc).First(), StringComparer.OrdinalIgnoreCase);

        var files = roots
            .Where(root => Directory.Exists(root.Path))
            .SelectMany(root => Directory.EnumerateFiles(root.Path, "*.*", SearchOption.AllDirectories)
                .Where(path => MarkdownExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
                .Select(path => Summary(project, path, root.Scope, artifactMap.GetValueOrDefault(Normalize(Path.GetRelativePath(project.RootPath, path))), cancellationToken)))
            .Select(task => task.GetAwaiter().GetResult())
            .Where(file => includeTechnical || !file.IsTechnical)
            .Where(file => scope is null || file.Scope == scope || scope == MarkdownWorkspaceScope.AllSafeMarkdown)
            .DistinctBy(file => file.RelativePath, StringComparer.OrdinalIgnoreCase)
            .OrderBy(file => file.Scope)
            .ThenBy(file => file.RelativePath)
            .ToArray();

        return files;
    }

    public async Task<MarkdownFileContent> ReadAsync(Project project, string relativePath, CancellationToken cancellationToken)
    {
        var validation = ValidatePath(project, relativePath, allowTechnical: true);
        if (!validation.IsValid) throw new InvalidOperationException(string.Join(" ", validation.Warnings));
        var fullPath = ToFullPath(project, relativePath);
        if (!File.Exists(fullPath)) throw new FileNotFoundException("Markdown file was not found.", fullPath);
        var summary = await Summary(project, fullPath, ScopeFor(Normalize(relativePath)), await artifacts.GetByPathAsync(project.Id, Normalize(relativePath), cancellationToken), cancellationToken);
        var markdown = await File.ReadAllTextAsync(fullPath, cancellationToken);
        return new MarkdownFileContent(summary, markdown, null, Hash(markdown), DateTimeOffset.UtcNow);
    }

    public async Task<MarkdownFileSaveResult> SaveAsync(Project project, MarkdownFileSaveRequest request, CancellationToken cancellationToken)
    {
        var relativePath = Normalize(request.RelativePath);
        var validation = ValidatePath(project, relativePath, allowTechnical: false);
        if (!validation.IsValid) throw new InvalidOperationException(string.Join(" ", validation.Warnings));

        var fullPath = ToFullPath(project, relativePath);
        if (File.Exists(fullPath) && !string.IsNullOrWhiteSpace(request.ExpectedContentHash))
        {
            var currentHash = Hash(await File.ReadAllTextAsync(fullPath, cancellationToken));
            if (!currentHash.Equals(request.ExpectedContentHash, StringComparison.OrdinalIgnoreCase))
            {
                return new MarkdownFileSaveResult(false, relativePath, currentHash, File.GetLastWriteTimeUtc(fullPath), "The file changed since it was loaded. Reload or force overwrite.", true);
            }
        }

        var artifact = await artifacts.GetByPathAsync(project.Id, relativePath, cancellationToken);
        if (artifact is { Status: GeneratedArtifactStatus.ManuallyModified } && request.SaveMode != MarkdownSaveMode.ForceOverwriteManualGenerated)
        {
            return new MarkdownFileSaveResult(false, relativePath, artifact.ContentHash, DateTimeOffset.UtcNow, "This generated file was manually modified. Confirm before overwriting.", true);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        var markdownWarnings = Validate(request.Markdown).Warnings;
        await File.WriteAllTextAsync(fullPath, request.Markdown, cancellationToken);
        var newHash = Hash(request.Markdown);
        var writtenUtc = File.GetLastWriteTimeUtc(fullPath);

        if (artifact is not null)
        {
            await artifacts.UpsertAsync(artifact with
            {
                ContentHash = newHash,
                SourceInputHash = newHash,
                GeneratedUtc = DateTimeOffset.UtcNow,
                Status = GeneratedArtifactStatus.Generated,
                Notes = "Saved explicitly in Markdown Workspace."
            }, cancellationToken);
        }

        return new MarkdownFileSaveResult(true, relativePath, newHash, writtenUtc, markdownWarnings.Count > 0 ? string.Join(" ", markdownWarnings) : null, false);
    }

    public MarkdownValidationResult Validate(string markdown) => mermaid.Validate(markdown);

    public MarkdownValidationResult ValidatePath(Project project, string relativePath, bool allowTechnical)
    {
        var normalized = Normalize(relativePath);
        if (string.IsNullOrWhiteSpace(normalized)) return Invalid("Markdown path is required.");
        if (!MarkdownExtensions.Contains(Path.GetExtension(normalized), StringComparer.OrdinalIgnoreCase)) return Invalid("Only .md and .markdown files can be edited.");
        if (BlockedNames.Any(name => normalized.Equals(name, StringComparison.OrdinalIgnoreCase) || normalized.EndsWith("/" + name, StringComparison.OrdinalIgnoreCase))) return Invalid("Secret-like files cannot be edited in SourceWorker.");
        if (normalized.Split('/').Any(part => BlockedFolders.Contains(part, StringComparer.OrdinalIgnoreCase))) return Invalid("Files in system/build folders cannot be edited.");
        if (!allowTechnical && IsTechnical(normalized)) return Invalid("Technical generated files are read-only in this MVP.");

        var root = Path.GetFullPath(project.RootPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var fullPath = ToFullPath(project, normalized);
        if (!fullPath.Equals(root, StringComparison.OrdinalIgnoreCase) &&
            !fullPath.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            return Invalid("Path traversal is not allowed.");
        }
        return new MarkdownValidationResult(true, []);
    }

    private static MarkdownValidationResult Invalid(string warning) => new(false, [warning]);

    private static async Task<MarkdownFileSummary> Summary(Project project, string fullPath, MarkdownWorkspaceScope scope, GeneratedArtifact? artifact, CancellationToken cancellationToken)
    {
        var info = new FileInfo(fullPath);
        var relativePath = Normalize(Path.GetRelativePath(project.RootPath, fullPath));
        var text = File.Exists(fullPath) ? await File.ReadAllTextAsync(fullPath, cancellationToken) : "";
        var kind = KindFor(relativePath);
        var isTechnical = IsTechnical(relativePath);
        var isExternal = relativePath.StartsWith("instructions/Result/", StringComparison.OrdinalIgnoreCase);
        return new MarkdownFileSummary(
            project.Id,
            relativePath,
            DisplayName(relativePath),
            kind,
            scope,
            info.Exists ? info.Length : 0,
            info.Exists ? info.LastWriteTimeUtc : DateTimeOffset.MinValue,
            artifact is not null,
            !isTechnical,
            true,
            isExternal,
            isTechnical,
            text.Contains("SourceWorker:Generated", StringComparison.OrdinalIgnoreCase),
            artifact?.Status == GeneratedArtifactStatus.ManuallyModified,
            artifact?.Status == GeneratedArtifactStatus.ManuallyModified ? "Generated file was manually modified." : null);
    }

    private static IReadOnlyList<(string Path, MarkdownWorkspaceScope Scope)> Roots(Project project, bool includeExternalMirror, bool includeUserMarkdown)
    {
        var roots = new List<(string, MarkdownWorkspaceScope)>
        {
            (Path.Combine(project.RootPath, ".ai-context", "docs", "backend"), MarkdownWorkspaceScope.BackendDocs),
            (Path.Combine(project.RootPath, ".ai-context", "instructions"), MarkdownWorkspaceScope.Instructions),
            (Path.Combine(project.RootPath, ".ai-context"), MarkdownWorkspaceScope.ProjectContext),
            (Path.Combine(project.RootPath, "instructions"), MarkdownWorkspaceScope.Instructions)
        };
        if (includeExternalMirror) roots.Add((Path.Combine(project.RootPath, "instructions", "Result", "Backend"), MarkdownWorkspaceScope.ExternalMirror));
        if (includeUserMarkdown) roots.Add((project.RootPath, MarkdownWorkspaceScope.UserMarkdown));
        return roots;
    }

    private static MarkdownWorkspaceScope ScopeFor(string relativePath)
        => relativePath.StartsWith(".ai-context/docs/backend/", StringComparison.OrdinalIgnoreCase) ? MarkdownWorkspaceScope.BackendDocs :
           relativePath.StartsWith(".ai-context/instructions/", StringComparison.OrdinalIgnoreCase) || relativePath.StartsWith("instructions/", StringComparison.OrdinalIgnoreCase) ? MarkdownWorkspaceScope.Instructions :
           relativePath.StartsWith("instructions/Result/Backend/", StringComparison.OrdinalIgnoreCase) ? MarkdownWorkspaceScope.ExternalMirror :
           relativePath.StartsWith(".ai-context/", StringComparison.OrdinalIgnoreCase) ? MarkdownWorkspaceScope.ProjectContext :
           MarkdownWorkspaceScope.UserMarkdown;

    private static MarkdownFileKind KindFor(string relativePath)
        => relativePath.Equals(".ai-context/agent-handoff.md", StringComparison.OrdinalIgnoreCase) ? MarkdownFileKind.AgentHandoff :
           relativePath.EndsWith("07-ai-explanation.md", StringComparison.OrdinalIgnoreCase) ? MarkdownFileKind.AiExplanation :
           relativePath.StartsWith(".ai-context/docs/backend/", StringComparison.OrdinalIgnoreCase) ? MarkdownFileKind.BackendDoc :
           relativePath.StartsWith(".ai-context/instructions/", StringComparison.OrdinalIgnoreCase) ? MarkdownFileKind.ImportedInstruction :
           relativePath.StartsWith("instructions/Result/Backend/", StringComparison.OrdinalIgnoreCase) ? MarkdownFileKind.ExternalMirrorDoc :
           IsTechnical(relativePath) ? MarkdownFileKind.TechnicalMarkdown :
           relativePath.StartsWith(".ai-context/", StringComparison.OrdinalIgnoreCase) ? MarkdownFileKind.ContextDoc :
           MarkdownFileKind.UserMarkdown;

    private static bool IsTechnical(string relativePath)
        => relativePath.Contains("/packs/", StringComparison.OrdinalIgnoreCase) ||
           relativePath.Contains("/recipes/", StringComparison.OrdinalIgnoreCase) ||
           relativePath.Contains("/exports/", StringComparison.OrdinalIgnoreCase) ||
           relativePath.Contains("/scans/", StringComparison.OrdinalIgnoreCase) ||
           relativePath.Contains("/branches/", StringComparison.OrdinalIgnoreCase) ||
           relativePath.Contains("/diagrams/", StringComparison.OrdinalIgnoreCase);

    private static string DisplayName(string relativePath) => Path.GetFileName(relativePath);
    private static string Normalize(string path) => path.Replace('\\', '/').TrimStart('/');
    private static string ToFullPath(Project project, string relativePath) => Path.GetFullPath(Path.Combine(project.RootPath, Normalize(relativePath).Replace('/', Path.DirectorySeparatorChar)));
    private static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
}

public sealed partial class MermaidMarkdownValidator : IMermaidMarkdownValidator
{
    private static readonly string[] ValidStarts = ["flowchart TD", "flowchart LR", "graph TD", "graph LR", "sequenceDiagram", "classDiagram", "stateDiagram", "erDiagram", "journey", "gantt", "pie", "mindmap"];

    public MarkdownValidationResult Validate(string markdown)
    {
        var warnings = new List<string>();
        var matches = MermaidBlockRegex().Matches(markdown);
        foreach (Match match in matches)
        {
            var body = match.Groups["body"].Value.Trim();
            if (string.IsNullOrWhiteSpace(body) || !ValidStarts.Any(start => body.StartsWith(start, StringComparison.OrdinalIgnoreCase)))
            {
                warnings.Add("Mermaid blocks should start with a valid diagram type such as flowchart TD or flowchart LR.");
            }
            foreach (var line in body.Split('\n'))
            {
                var nodeMatch = Regex.Match(line, @"^\s*(?<id>[^\[\]\s]+)\[");
                if (!nodeMatch.Success) continue;
                var nodeId = nodeMatch.Groups["id"].Value;
                if (nodeId.Contains('/') || nodeId.Contains('\\') || nodeId.Contains('.'))
                {
                    warnings.Add("Mermaid node ids should be simple tokens without spaces or file paths.");
                    break;
                }
            }
        }

        if (markdown.Contains("```mermaid", StringComparison.OrdinalIgnoreCase) && !HasClosedMermaidFence(markdown))
        {
            warnings.Add("A Mermaid block appears to be missing its closing triple backticks.");
        }

        if (RawMermaidRegex().IsMatch(markdown) && matches.Count == 0)
        {
            warnings.Add("Mermaid content should be fenced with ```mermaid.");
        }

        return new MarkdownValidationResult(true, warnings);
    }

    private static bool HasClosedMermaidFence(string markdown)
    {
        var index = markdown.IndexOf("```mermaid", StringComparison.OrdinalIgnoreCase);
        return index < 0 || markdown.IndexOf("```", index + "```mermaid".Length, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    [GeneratedRegex(@"```mermaid\s*(?<body>[\s\S]*?)```", RegexOptions.IgnoreCase)]
    private static partial Regex MermaidBlockRegex();

    [GeneratedRegex(@"(^|\n)\s*(flowchart\s+(TD|LR)|graph\s+(TD|LR)|sequenceDiagram)\b", RegexOptions.IgnoreCase)]
    private static partial Regex RawMermaidRegex();
}
