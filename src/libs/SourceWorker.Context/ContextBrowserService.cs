using SourceWorker.Application;
using SourceWorker.Domain;

namespace SourceWorker.Context;

public sealed class ContextBrowserService(IRecipeRepository recipes, ICodeFlowRepository flows, IRouteInfoRepository routes, IGeneratedArtifactRepository artifacts) : IContextBrowserService, IContextSearchService
{
    public async Task<ContextBrowserSummary> GetSummaryAsync(Project project, CancellationToken cancellationToken)
        => new(project.Id, await ListFilesAsync(project, cancellationToken), await recipes.ListAsync(project, cancellationToken), await flows.GetByProjectAsync(project.Id, cancellationToken), await routes.GetByProjectAsync(project.Id, cancellationToken), [], (await artifacts.GetByProjectAsync(project.Id, cancellationToken)).Where(a => a.Status != GeneratedArtifactStatus.Generated).ToArray());

    public Task<IReadOnlyList<ContextBrowserFile>> ListFilesAsync(Project project, CancellationToken cancellationToken)
    {
        var roots = AllowedRoots(project);
        var files = roots.Where(Directory.Exists)
            .SelectMany(root => Directory.EnumerateFiles(root, "*.md", SearchOption.AllDirectories).Concat(Directory.EnumerateFiles(root, "*.yaml", SearchOption.AllDirectories)).Concat(Directory.EnumerateFiles(root, "*.mmd", SearchOption.AllDirectories)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(path => new FileInfo(path))
            .Select(info => new ContextBrowserFile(Path.GetRelativePath(project.RootPath, info.FullName).Replace('\\', '/'), info.FullName, Group(Path.GetRelativePath(project.RootPath, info.FullName).Replace('\\', '/')), info.Length, info.LastWriteTimeUtc))
            .OrderBy(f => f.RelativePath)
            .ToArray();
        return Task.FromResult<IReadOnlyList<ContextBrowserFile>>(files);
    }

    public async Task<string> ReadFileAsync(Project project, string relativePath, CancellationToken cancellationToken)
    {
        var normalized = relativePath.Replace('\\', '/').TrimStart('/');
        var fullPath = Path.GetFullPath(Path.Combine(project.RootPath, normalized));
        var allowed = AllowedRoots(project).Any(root => fullPath.StartsWith(Path.GetFullPath(root), StringComparison.OrdinalIgnoreCase));
        if (!allowed || !File.Exists(fullPath)) throw new InvalidOperationException("Context file path is not allowed.");
        return await File.ReadAllTextAsync(fullPath, cancellationToken);
    }

    public async Task<ContextBrowserQueryResult> SearchAsync(Project project, string query, CancellationToken cancellationToken)
    {
        var hits = new List<ContextBrowserSearchHit>();
        foreach (var file in await ListFilesAsync(project, cancellationToken))
        {
            var lines = await File.ReadAllLinesAsync(file.AbsolutePath, cancellationToken);
            for (var i = 0; i < lines.Length; i++)
                if (lines[i].Contains(query, StringComparison.OrdinalIgnoreCase))
                    hits.Add(new ContextBrowserSearchHit(file.RelativePath, i + 1, lines[i].Trim()));
        }
        return new ContextBrowserQueryResult(query, hits.Take(100).ToArray());
    }

    private static string[] AllowedRoots(Project project) => [Path.Combine(project.RootPath, ".ai-context"), Path.Combine(project.RootPath, "instructions", "Result", "Backend")];

    private static string Group(string relativePath)
        => relativePath switch
        {
            var path when path.StartsWith(".ai-context/instructions/", StringComparison.OrdinalIgnoreCase) => "Instructions",
            var path when path.StartsWith(".ai-context/docs/", StringComparison.OrdinalIgnoreCase) => "Generated Docs",
            var path when path.StartsWith(".ai-context/recipes/", StringComparison.OrdinalIgnoreCase) => "Recipes",
            var path when path.StartsWith(".ai-context/packs/", StringComparison.OrdinalIgnoreCase) => "Packs",
            var path when path.StartsWith(".ai-context/exports/", StringComparison.OrdinalIgnoreCase) => "Exports",
            var path when path.StartsWith(".ai-context/diagrams/", StringComparison.OrdinalIgnoreCase) => "Diagrams",
            var path when path.StartsWith("instructions/Result/Backend/", StringComparison.OrdinalIgnoreCase) => "External Outputs",
            _ => "Root Context"
        };
}
