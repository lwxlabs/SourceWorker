using System.Text;
using SourceWorker.Application;
using SourceWorker.Domain;

namespace SourceWorker.Context;

public sealed class MarkdownContextWriter : IContextWriter
{
    public async Task<ContextWriteResult> WriteBasicContextAsync(ProjectScanResult scanResult, CancellationToken cancellationToken)
    {
        var output = Path.Combine(scanResult.Project.RootPath, ".ai-context");
        Directory.CreateDirectory(output);

        var files = new Dictionary<string, string>
        {
            ["CONTEXT.md"] = RootContext(scanResult),
            ["project.md"] = Project(scanResult),
            ["current-state.md"] = CurrentState(scanResult),
            ["tasks.md"] = Tasks(scanResult),
            ["ai-handoff.md"] = Handoff(scanResult),
            ["index.yaml"] = Index(scanResult)
        };

        foreach (var file in files)
        {
            await File.WriteAllTextAsync(Path.Combine(output, file.Key), file.Value, cancellationToken);
        }

        return new ContextWriteResult(output, files.Keys.ToArray());
    }

    private static string RootContext(ProjectScanResult scan)
        => $"""
        # {scan.Project.Name}

        SourceWorker generated local AI context for this codebase.

        - Project path: `{scan.Project.RootPath}`
        - Last scan: {scan.ScanRun.CompletedAt:O}
        - Files indexed: {scan.Files.Count}
        - TODO/FIXME/REVIEW items: {scan.Todos.Count}
        """;

    private static string Project(ProjectScanResult scan)
        => $"""
        # Project

        ## Technologies

        {ListOrNone(scan.Technologies.Select(t => $"- {t.Kind}: `{t.Evidence}`"))}

        ## Key Files

        {ListOrNone(scan.Files.Where(f => f.Kind is IndexedFileKind.Solution or IndexedFileKind.Project or IndexedFileKind.Markdown).Select(f => $"- `{f.RelativePath}`"))}
        """;

    private static string CurrentState(ProjectScanResult scan)
        => $"""
        # Current State

        - Git branch: {scan.Git.Branch ?? "unknown"}
        - Git commit: {scan.Git.CommitSha ?? "unknown"}
        - Working tree dirty: {scan.Git.IsDirty}
        - Scan status: {scan.ScanRun.Status}
        """;

    private static string Tasks(ProjectScanResult scan)
        => $"""
        # Tasks

        {ListOrNone(scan.Todos.Select(t => $"- {t.Kind} `{t.RelativePath}:{t.LineNumber}` {t.Text}"))}
        """;

    private static string Handoff(ProjectScanResult scan)
        => $"""
        # AI Handoff

        This is a local-first codebase context bundle. Start with `project.md`, then check `current-state.md` and `tasks.md`.

        Focus areas from the scan:

        {ListOrNone(scan.Technologies.Select(t => $"- {t.Kind}"))}
        """;

    private static string Index(ProjectScanResult scan)
    {
        var builder = new StringBuilder();
        builder.AppendLine("version: 1");
        builder.AppendLine($"projectId: {scan.Project.Id}");
        builder.AppendLine($"scanRunId: {scan.ScanRun.Id}");
        builder.AppendLine("files:");
        foreach (var file in scan.Files.OrderBy(f => f.RelativePath))
        {
            builder.AppendLine($"  - path: \"{file.RelativePath.Replace("\"", "\\\"")}\"");
            builder.AppendLine($"    kind: {file.Kind}");
            builder.AppendLine($"    sizeBytes: {file.SizeBytes}");
        }

        return builder.ToString();
    }

    private static string ListOrNone(IEnumerable<string> lines)
    {
        var materialized = lines.ToArray();
        return materialized.Length == 0 ? "- None detected" : string.Join(Environment.NewLine, materialized);
    }
}
