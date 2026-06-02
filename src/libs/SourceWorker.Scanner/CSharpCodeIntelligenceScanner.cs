using System.Text.RegularExpressions;
using SourceWorker.Application;
using SourceWorker.Domain;

namespace SourceWorker.Scanner;

public sealed partial class CSharpCodeIntelligenceScanner(IFlowExtractor flowExtractor) : ICSharpCodeIntelligenceScanner
{
    public async Task<CodeIntelligenceResult> ScanAsync(ProjectScanResult scanResult, CancellationToken cancellationToken)
    {
        var symbols = new List<CodeSymbol>();
        var routes = new List<RouteInfo>();
        var services = new List<ServiceRegistration>();
        var configs = new List<ConfigurationUsage>();
        var workers = new List<BackgroundWorkerInfo>();
        var tests = new List<TestInfo>();

        foreach (var file in scanResult.Files.Where(f => f.Extension.Equals(".cs", StringComparison.OrdinalIgnoreCase)))
        {
            var lines = await File.ReadAllLinesAsync(file.Path, cancellationToken);
            var ns = "";
            var currentType = "";
            var attrs = new List<string>();
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                var nsMatch = NamespaceRegex().Match(line);
                if (nsMatch.Success) ns = nsMatch.Groups["name"].Value;
                if (line.StartsWith("[") && line.EndsWith("]")) attrs.Add(line.Trim('[', ']'));

                var type = TypeRegex().Match(line);
                if (type.Success)
                {
                    currentType = type.Groups["name"].Value;
                    var kind = Enum.Parse<CodeSymbolKind>(FirstUpper(type.Groups["kind"].Value), ignoreCase: true);
                    symbols.Add(new CodeSymbol(Guid.NewGuid(), scanResult.Project.Id, scanResult.ScanRun.Id, file.Path, file.RelativePath, ns, currentType, string.IsNullOrWhiteSpace(ns) ? currentType : $"{ns}.{currentType}", kind, type.Groups["access"].Value, null, null, [], i + 1, attrs.ToArray(), line.Contains(" static "), false));
                    if (line.Contains("BackgroundService", StringComparison.Ordinal) || line.Contains("IHostedService", StringComparison.Ordinal))
                        workers.Add(new BackgroundWorkerInfo(Guid.NewGuid(), scanResult.Project.Id, scanResult.ScanRun.Id, file.RelativePath, currentType, line.Contains("BackgroundService") ? "BackgroundService" : "IHostedService", i + 1));
                    attrs.Clear();
                    continue;
                }

                var method = MethodRegex().Match(line);
                if (method.Success && !line.StartsWith("if ") && !line.StartsWith("switch "))
                {
                    var name = method.Groups["name"].Value;
                    symbols.Add(new CodeSymbol(Guid.NewGuid(), scanResult.Project.Id, scanResult.ScanRun.Id, file.Path, file.RelativePath, ns, name, string.IsNullOrWhiteSpace(currentType) ? name : $"{currentType}.{name}", CodeSymbolKind.Method, method.Groups["access"].Value, currentType, method.Groups["return"].Value, SplitParameters(method.Groups["params"].Value), i + 1, attrs.ToArray(), line.Contains(" static "), method.Groups["async"].Success));
                    foreach (var attr in attrs.Where(a => a.StartsWith("Http", StringComparison.OrdinalIgnoreCase) || a.StartsWith("Route", StringComparison.OrdinalIgnoreCase)))
                        routes.Add(new RouteInfo(Guid.NewGuid(), scanResult.Project.Id, scanResult.ScanRun.Id, file.RelativePath, HttpMethod(attr), RoutePattern(attr), $"{currentType}.{name}", i + 1));
                    if (attrs.Any(a => a is "Fact" or "Theory" or "Test"))
                        tests.Add(new TestInfo(Guid.NewGuid(), scanResult.Project.Id, scanResult.ScanRun.Id, file.RelativePath, currentType, name, attrs.Contains("Fact") || attrs.Contains("Theory") ? "xUnit" : "Unknown", i + 1));
                    attrs.Clear();
                }

                var minimal = MinimalRouteRegex().Match(line);
                if (minimal.Success)
                    routes.Add(new RouteInfo(Guid.NewGuid(), scanResult.Project.Id, scanResult.ScanRun.Id, file.RelativePath, minimal.Groups["method"].Value.Replace("Map", "").ToUpperInvariant(), minimal.Groups["route"].Value, null, i + 1));

                var service = ServiceRegex().Match(line);
                if (service.Success)
                    services.Add(new ServiceRegistration(Guid.NewGuid(), scanResult.Project.Id, scanResult.ScanRun.Id, file.RelativePath, service.Groups["method"].Value, service.Groups["service"].Value, service.Groups["impl"].Success ? service.Groups["impl"].Value : null, i + 1));

                var config = ConfigRegex().Match(line);
                if (config.Success)
                    configs.Add(new ConfigurationUsage(Guid.NewGuid(), scanResult.Project.Id, scanResult.ScanRun.Id, file.RelativePath, config.Groups["key"].Value, config.Groups["kind"].Value, i + 1));
            }
        }

        var temp = new CodeIntelligenceResult(symbols, routes, services, configs, workers, tests, []);
        var flows = flowExtractor.Extract(scanResult, temp);
        return temp with { Flows = flows };
    }

    private static IReadOnlyList<string> SplitParameters(string value) => string.IsNullOrWhiteSpace(value) ? [] : value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
    private static string FirstUpper(string value) => string.IsNullOrWhiteSpace(value) ? value : char.ToUpperInvariant(value[0]) + value[1..];
    private static string HttpMethod(string attr) => attr.StartsWith("Http", StringComparison.OrdinalIgnoreCase) ? attr.Split('(', 2)[0].Replace("Http", "").ToUpperInvariant() : "ANY";
    private static string RoutePattern(string attr) { var m = Regex.Match(attr, "\"(?<r>[^\"]+)\""); return m.Success ? m.Groups["r"].Value : ""; }

    [GeneratedRegex(@"^namespace\s+(?<name>[A-Za-z0-9_.]+)")]
    private static partial Regex NamespaceRegex();
    [GeneratedRegex(@"(?<access>public|internal|private|protected)?\s*(?:sealed\s+|static\s+|abstract\s+|partial\s+)*(?<kind>class|interface|record|struct|enum)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)")]
    private static partial Regex TypeRegex();
    [GeneratedRegex(@"(?<access>public|internal|private|protected)\s+(?<async>async\s+)?(?<return>[A-Za-z0-9_<>,.?]+)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\((?<params>[^)]*)\)")]
    private static partial Regex MethodRegex();
    [GeneratedRegex(@"\.?(?<method>MapGet|MapPost|MapPut|MapDelete|MapPatch)\s*\(\s*""(?<route>[^""]+)""")]
    private static partial Regex MinimalRouteRegex();
    [GeneratedRegex(@"services\.(?<method>Add[A-Za-z]+)\s*<(?<service>[^,>]+)(?:,\s*(?<impl>[^>]+))?")]
    private static partial Regex ServiceRegex();
    [GeneratedRegex(@"(?<kind>GetSection|GetValue|Bind)\s*(?:<[^>]+>)?\s*\(\s*""(?<key>[^""]+)""")]
    private static partial Regex ConfigRegex();
}

public sealed class RuleBasedFlowExtractor : IFlowExtractor
{
    public IReadOnlyList<CodeFlow> Extract(ProjectScanResult scan, CodeIntelligenceResult code)
    {
        var files = scan.Files.Select(f => f.RelativePath).ToArray();
        var symbols = code.Symbols.Select(s => s.FullName).ToArray();
        var flows = new List<CodeFlow>();
        var isSourceWorker = scan.Project.Name.Equals("SourceWorker", StringComparison.OrdinalIgnoreCase) ||
            scan.Projects.Any(p => p.Name.StartsWith("SourceWorker.", StringComparison.OrdinalIgnoreCase));

        if (isSourceWorker)
        {
            Add("Scan Project", CodeFlowType.ScanFlow, "CLI scan <projectPath> or POST /api/projects/{id}/scan", ["CLI/API receives scan request", "Application command runs scan", "Scanner indexes files and technologies", "Localite persists scan data", "Context writer updates .ai-context", "Recipe runner writes backend docs"], FlowOrigin.SourceWorkerInternal, FlowConfidence.High);
            Add("Run Recipe", CodeFlowType.RecipeRunFlow, "CLI run-recipe <projectPath> <recipeId>", ["Application loads recipe", "Scanner refreshes project data", "Recipe runner writes generated docs", "Generated artifacts are tracked"], FlowOrigin.SourceWorkerInternal, FlowConfidence.High);
            Add("Artifact Cleanup", CodeFlowType.ArtifactCleanupFlow, "CLI cleanup-preview or cleanup-generated", ["Lifecycle service analyzes generated files", "Cleanup preview lists safe candidates", "Cleanup deletes only generated orphan/deprecated files"], FlowOrigin.SourceWorkerInternal, FlowConfidence.High);
            Add("Storage Info", CodeFlowType.StorageInfoFlow, "CLI storage-info or GET /api/storage/info", ["Storage info service reads schema", "Repository counts are returned"], FlowOrigin.SourceWorkerInternal, FlowConfidence.High);
        }

        if (code.Routes.Count > 0) Add("Local API Endpoint Flow", CodeFlowType.LocalApiEndpointFlow, "HTTP request", ["Minimal API route receives request", "Application request handler executes", "Repository/service returns DTO"], FlowOrigin.SourceCodeDetected, FlowConfidence.High);
        if (code.BackgroundWorkers.Count > 0) Add("Worker Job Flow", CodeFlowType.WorkerJobFlow, "Queued worker job", ["Job queue dequeues id", "Worker loads project", "Worker executes worker logic", "Worker persists progress"], FlowOrigin.SourceCodeDetected, FlowConfidence.High);

        foreach (var domainFlow in InferDomainFlows(scan, code))
            flows.Add(domainFlow);

        return flows;

        void Add(string name, CodeFlowType type, string trigger, IReadOnlyList<string> steps, FlowOrigin origin, FlowConfidence confidence)
            => flows.Add(new CodeFlow(Guid.NewGuid(), scan.Project.Id, scan.ScanRun.Id, name, type, trigger, files.Where(f => f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)).Take(12).ToArray(), symbols.Take(20).ToArray(), steps.Select((s, i) => new CodeFlowStep(i + 1, s, null, null)).ToArray(), confidence, [], origin));
    }

    private static IReadOnlyList<CodeFlow> InferDomainFlows(ProjectScanResult scan, CodeIntelligenceResult code)
    {
        var candidates = new[]
        {
            ("Create context", new[] { "Context", "Create", "Open" }),
            ("Ensure schema", new[] { "Schema", "Ensure", "Migration" }),
            ("Query data", new[] { "Query", "Find", "Get", "Read" }),
            ("Save graph", new[] { "Save", "Insert", "Update", "Write" }),
            ("Delete graph", new[] { "Delete", "Remove" }),
            ("Export/import data", new[] { "Export", "Import" }),
            ("Cache get/set", new[] { "Cache", "Get", "Set" }),
            ("Secure local handling", new[] { "Secure", "Secret", "Encrypt", "Credential" })
        };

        var searchable = code.Symbols.Select(s => $"{s.Name} {s.FullName} {s.RelativePath}")
            .Concat(code.Tests.Select(t => $"{t.ClassName} {t.MethodName} {t.RelativePath}"))
            .Concat(scan.Files.Select(f => f.RelativePath))
            .ToArray();

        var flows = new List<CodeFlow>();
        foreach (var (name, terms) in candidates)
        {
            var matched = searchable.Where(value => terms.Any(term => value.Contains(term, StringComparison.OrdinalIgnoreCase))).Take(10).ToArray();
            if (matched.Length == 0) continue;
            var relatedFiles = code.Symbols.Where(s => terms.Any(term => s.Name.Contains(term, StringComparison.OrdinalIgnoreCase) || s.RelativePath.Contains(term, StringComparison.OrdinalIgnoreCase))).Select(s => s.RelativePath)
                .Concat(code.Tests.Where(t => terms.Any(term => t.MethodName.Contains(term, StringComparison.OrdinalIgnoreCase) || t.RelativePath.Contains(term, StringComparison.OrdinalIgnoreCase))).Select(t => t.RelativePath))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(10)
                .ToArray();
            var relatedSymbols = code.Symbols.Where(s => terms.Any(term => s.Name.Contains(term, StringComparison.OrdinalIgnoreCase))).Select(s => s.FullName).Take(12).ToArray();
            flows.Add(new CodeFlow(
                Guid.NewGuid(),
                scan.Project.Id,
                scan.ScanRun.Id,
                name,
                CodeFlowType.CandidateDomainFlow,
                "Inferred from public symbols, test names, and feature folders",
                relatedFiles,
                relatedSymbols,
                [new CodeFlowStep(1, "Identify related public API, tests, or feature folders", relatedSymbols.FirstOrDefault(), relatedFiles.FirstOrDefault()), new CodeFlowStep(2, "Inspect matching files before making changes", null, relatedFiles.FirstOrDefault())],
                matched.Length >= 4 ? FlowConfidence.Medium : FlowConfidence.Low,
                ["Candidate domain flow inferred without AI; inspect files to confirm."],
                relatedSymbols.Length > 0 ? FlowOrigin.SourceCodeDetected : FlowOrigin.TestInferred));
        }

        return flows;
    }
}

public sealed class FlowDiagramGenerator : IFlowDiagramGenerator
{
    public string Generate(CodeFlow flow)
    {
        var lines = new List<string> { "flowchart TD" };
        for (var i = 0; i < flow.Steps.Count; i++)
        {
            var id = ((char)('A' + i)).ToString();
            lines.Add($"    {id}[{Escape(flow.Steps[i].Text)}]");
            if (i > 0) lines.Add($"    {(char)('A' + i - 1)} --> {id}");
        }
        return string.Join(Environment.NewLine, lines);
    }
    private static string Escape(string value) => value.Replace("[", "(").Replace("]", ")");
}
