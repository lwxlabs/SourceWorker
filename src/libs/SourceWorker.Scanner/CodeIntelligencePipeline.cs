using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using SourceWorker.Application;
using SourceWorker.Domain;
using static SourceWorker.Scanner.AnalyzerResultFactory;

namespace SourceWorker.Scanner;

public sealed class CodeAnalyzerRegistry(IEnumerable<ICodeAnalyzer> analyzers) : ICodeAnalyzerRegistry
{
    private readonly ICodeAnalyzer[] _analyzers = analyzers.OrderBy(a => a.Descriptor.Id, StringComparer.OrdinalIgnoreCase).ToArray();
    public IReadOnlyList<CodeAnalyzerDescriptor> ListDescriptors() => _analyzers.Select(a => a.Descriptor).ToArray();
    public IReadOnlyList<ICodeAnalyzer> ListAnalyzers() => _analyzers;
    public ICodeAnalyzer? GetAnalyzer(string analyzerId) => _analyzers.FirstOrDefault(a => a.Descriptor.Id.Equals(analyzerId, StringComparison.OrdinalIgnoreCase));
}

public sealed class CodeAnalysisSettingsService(ICodeAnalysisSettingsRepository? repository, ICodeAnalyzerRegistry registry) : ICodeAnalysisSettingsService
{
    public async Task<CodeAnalysisSettings> GetAsync(Guid projectId, CancellationToken cancellationToken)
    {
        if (repository is not null && await repository.GetAsync(projectId, cancellationToken) is { } saved)
            return Normalize(saved);
        return CreateDefault(projectId);
    }

    public async Task<CodeAnalysisSettings> SaveAsync(CodeAnalysisSettings settings, CancellationToken cancellationToken)
    {
        var normalized = Normalize(settings with { UpdatedUtc = DateTimeOffset.UtcNow });
        return repository is null ? normalized : await repository.SaveAsync(normalized, cancellationToken);
    }

    private CodeAnalysisSettings CreateDefault(Guid projectId)
        => new(projectId, registry.ListDescriptors().ToDictionary(d => d.Id, d => !d.IsExperimental, StringComparer.OrdinalIgnoreCase), UpdatedUtc: DateTimeOffset.UtcNow);

    private CodeAnalysisSettings Normalize(CodeAnalysisSettings settings)
    {
        var map = new Dictionary<string, bool>(settings.AnalyzerEnabled, StringComparer.OrdinalIgnoreCase);
        foreach (var descriptor in registry.ListDescriptors())
        {
            if (!map.ContainsKey(descriptor.Id)) map[descriptor.Id] = !descriptor.IsExperimental;
        }

        return settings with { AnalyzerEnabled = map, UpdatedUtc = settings.UpdatedUtc == default ? DateTimeOffset.UtcNow : settings.UpdatedUtc };
    }
}

public sealed class CodeIntelligencePipeline(ICodeAnalyzerRegistry registry, ILanguageDetectionService languageDetection) : ICodeIntelligencePipeline
{
    public async Task<CodeIntelligenceResult> AnalyzeAsync(ProjectScanResult scanResult, CodeAnalysisSettings settings, CancellationToken cancellationToken)
    {
        var results = new List<CodeAnalyzerResult>();
        foreach (var analyzer in registry.ListAnalyzers())
        {
            if (settings.AnalyzerEnabled.TryGetValue(analyzer.Descriptor.Id, out var enabled) && !enabled) continue;
            if (!analyzer.CanAnalyze(scanResult)) continue;
            results.Add(await analyzer.AnalyzeAsync(scanResult, settings, cancellationToken));
        }

        var profile = languageDetection.Detect(scanResult, results);
        return new CodeIntelligenceResult(
            results.SelectMany(r => r.Symbols).ToArray(),
            results.SelectMany(r => r.Routes).ToArray(),
            results.SelectMany(r => r.ServiceRegistrations).ToArray(),
            results.SelectMany(r => r.ConfigurationUsages).ToArray(),
            results.SelectMany(r => r.BackgroundWorkers).ToArray(),
            results.SelectMany(r => r.Tests).ToArray(),
            results.SelectMany(r => r.Flows).ToArray())
        {
            Components = results.SelectMany(r => r.Components).ToArray(),
            FrontendRoutes = results.SelectMany(r => r.FrontendRoutes).ToArray(),
            Styles = results.SelectMany(r => r.Styles).ToArray(),
            Imports = results.SelectMany(r => r.Imports).ToArray(),
            PackageReferences = results.SelectMany(r => r.PackageReferences).ToArray(),
            BuildScripts = results.SelectMany(r => r.BuildScripts).ToArray(),
            AnalyzerRuns = results.Select(r => r.Run).ToArray(),
            LanguageProfile = profile,
            Technologies = scanResult.Technologies.Concat(results.SelectMany(r => r.Technologies)).DistinctBy(t => $"{t.Kind}:{t.Evidence}").ToArray()
        };
    }
}

public sealed class LanguageDetectionService : ILanguageDetectionService
{
    public ProjectLanguageProfile Detect(ProjectScanResult scanResult, IReadOnlyList<CodeAnalyzerResult> analyzerResults)
    {
        var fileCounts = scanResult.Files
            .Select(f => ExtensionToLanguage(f.RelativePath, f.Extension))
            .Where(l => l != CodeLanguage.Unknown)
            .GroupBy(l => l)
            .ToDictionary(g => g.Key, g => g.Count());

        foreach (var analyzer in analyzerResults)
        {
            if (analyzer.Analyzer.Language != CodeLanguage.Unknown && !fileCounts.ContainsKey(analyzer.Analyzer.Language))
                fileCounts[analyzer.Analyzer.Language] = analyzer.Run.FilesAnalyzed;
        }

        var frameworks = new Dictionary<CodeFramework, int>();
        AddFramework(frameworks, CodeFramework.DotNet, scanResult.Projects.Count);
        if (scanResult.Technologies.Any(t => t.Kind == TechnologyKind.AspNetCore)) AddFramework(frameworks, CodeFramework.AspNetCore, 1);
        if (scanResult.Technologies.Any(t => t.Kind == TechnologyKind.Blazor)) AddFramework(frameworks, CodeFramework.Blazor, 1);
        if (scanResult.Technologies.Any(t => t.Kind == TechnologyKind.Maui)) AddFramework(frameworks, CodeFramework.Maui, 1);
        foreach (var package in analyzerResults.SelectMany(r => r.PackageReferences))
            AddFramework(frameworks, PackageToFramework(package.Name), 1);
        foreach (var component in analyzerResults.SelectMany(r => r.Components))
            if (Enum.TryParse<CodeFramework>(component.Framework, ignoreCase: true, out var framework)) AddFramework(frameworks, framework, 1);

        return new ProjectLanguageProfile(scanResult.Project.Id, scanResult.ScanRun.Id, fileCounts.Keys.OrderBy(k => k.ToString()).ToArray(), frameworks.Keys.Where(f => f != CodeFramework.Unknown).OrderBy(f => f.ToString()).ToArray(), fileCounts, frameworks.Where(kvp => kvp.Key != CodeFramework.Unknown).ToDictionary(), [], DateTimeOffset.UtcNow);
    }

    internal static CodeLanguage ExtensionToLanguage(string relativePath, string extension)
    {
        if (relativePath.EndsWith("Dockerfile", StringComparison.OrdinalIgnoreCase)) return CodeLanguage.Dockerfile;
        if (relativePath.StartsWith(".github/workflows/", StringComparison.OrdinalIgnoreCase)) return CodeLanguage.GitHubActions;
        return extension.ToLowerInvariant() switch
        {
            ".cs" => CodeLanguage.CSharp,
            ".razor" or ".cshtml" => CodeLanguage.Razor,
            ".js" or ".jsx" or ".mjs" or ".cjs" => CodeLanguage.JavaScript,
            ".ts" or ".tsx" => CodeLanguage.TypeScript,
            ".vue" => CodeLanguage.Vue,
            ".html" or ".htm" => CodeLanguage.Html,
            ".css" or ".scss" or ".sass" or ".less" => CodeLanguage.Css,
            ".json" => CodeLanguage.Json,
            ".yaml" or ".yml" => CodeLanguage.Yaml,
            ".xml" => CodeLanguage.Xml,
            _ => CodeLanguage.Unknown
        };
    }

    internal static CodeFramework PackageToFramework(string package) => package.ToLowerInvariant() switch
    {
        var p when p.Contains("microsoft.aspnetcore") => CodeFramework.AspNetCore,
        var p when p.Contains("microsoft.maui") => CodeFramework.Maui,
        var p when p.Contains("blazor") => CodeFramework.Blazor,
        var p when p is "vue" or "@vitejs/plugin-vue" => CodeFramework.Vue,
        var p when p.StartsWith("@angular/") => CodeFramework.Angular,
        var p when p is "react" or "react-dom" => CodeFramework.React,
        var p when p is "express" => CodeFramework.Express,
        var p when p is "vite" => CodeFramework.Vite,
        var p when p is "webpack" => CodeFramework.Webpack,
        var p when p.Contains("tailwind") => CodeFramework.Tailwind,
        var p when p is "typescript" => CodeFramework.TypeScript,
        _ => CodeFramework.Unknown
    };

    private static void AddFramework(Dictionary<CodeFramework, int> frameworks, CodeFramework framework, int count)
    {
        if (framework == CodeFramework.Unknown || count <= 0) return;
        frameworks[framework] = frameworks.GetValueOrDefault(framework) + count;
    }
}

public sealed class CSharpCodeAnalyzer(CSharpCodeIntelligenceScanner scanner) : ICodeAnalyzer
{
    public CodeAnalyzerDescriptor Descriptor { get; } = new("csharp", "C# Analyzer", CodeLanguage.CSharp, AnalyzerState.Enabled, [AnalyzerCapability.Symbols, AnalyzerCapability.Routes, AnalyzerCapability.Configuration, AnalyzerCapability.Tests, AnalyzerCapability.Flows], ["**/*.cs"]);
    public bool CanAnalyze(ProjectScanResult scanResult) => scanResult.Files.Any(f => f.Extension.Equals(".cs", StringComparison.OrdinalIgnoreCase));

    public async Task<CodeAnalyzerResult> AnalyzeAsync(ProjectScanResult scanResult, CodeAnalysisSettings settings, CancellationToken cancellationToken)
    {
        var started = DateTimeOffset.UtcNow;
        var sw = Stopwatch.StartNew();
        try
        {
            var result = await scanner.ScanAsync(scanResult, cancellationToken);
            var analyzed = scanResult.Files.Count(f => f.Extension.Equals(".cs", StringComparison.OrdinalIgnoreCase));
            var run = Run(scanResult, Descriptor, AnalyzerState.Enabled, analyzed, analyzed, result.Symbols.Count + result.Routes.Count + result.Tests.Count + result.Flows.Count, sw.Elapsed, [], null, started);
            return Empty(Descriptor, run) with { Symbols = result.Symbols, Routes = result.Routes, ServiceRegistrations = result.ServiceRegistrations, ConfigurationUsages = result.ConfigurationUsages, BackgroundWorkers = result.BackgroundWorkers, Tests = result.Tests, Flows = result.Flows };
        }
        catch (Exception ex)
        {
            var run = Run(scanResult, Descriptor, AnalyzerState.Failed, 0, 0, 0, sw.Elapsed, [], ex.Message, started);
            return Empty(Descriptor, run);
        }
    }
}

public abstract class TextCodeAnalyzerBase : ICodeAnalyzer
{
    public abstract CodeAnalyzerDescriptor Descriptor { get; }
    public abstract bool CanAnalyze(ProjectScanResult scanResult);
    protected abstract Task FillAsync(ProjectScanResult scan, CodeAnalysisSettings settings, AnalyzerBuffer buffer, CancellationToken cancellationToken);

    public async Task<CodeAnalyzerResult> AnalyzeAsync(ProjectScanResult scanResult, CodeAnalysisSettings settings, CancellationToken cancellationToken)
    {
        var started = DateTimeOffset.UtcNow;
        var sw = Stopwatch.StartNew();
        var buffer = new AnalyzerBuffer(scanResult, Descriptor);
        try
        {
            await FillAsync(scanResult, settings, buffer, cancellationToken);
            var run = Run(scanResult, Descriptor, AnalyzerState.Enabled, buffer.FilesConsidered, buffer.FilesAnalyzed, buffer.ItemCount, sw.Elapsed, buffer.Warnings, null, started);
            return buffer.ToResult(run);
        }
        catch (Exception ex)
        {
            var run = Run(scanResult, Descriptor, AnalyzerState.Failed, buffer.FilesConsidered, buffer.FilesAnalyzed, buffer.ItemCount, sw.Elapsed, buffer.Warnings, ex.Message, started);
            return buffer.ToResult(run);
        }
    }

    protected IEnumerable<IndexedFile> CandidateFiles(ProjectScanResult scan, CodeAnalysisSettings settings, params string[] extensions)
        => scan.Files
            .Where(f => extensions.Any(e => f.Extension.Equals(e, StringComparison.OrdinalIgnoreCase)) || extensions.Any(e => f.RelativePath.EndsWith(e, StringComparison.OrdinalIgnoreCase)))
            .Where(f => settings.IncludeGeneratedFiles || !IsGeneratedPath(f.RelativePath))
            .Where(f => settings.IncludeNodeModules || !f.RelativePath.Contains("node_modules/", StringComparison.OrdinalIgnoreCase) && !f.RelativePath.Contains("node_modules\\", StringComparison.OrdinalIgnoreCase))
            .Where(f => settings.IncludeDist || !f.RelativePath.Contains("/dist/", StringComparison.OrdinalIgnoreCase) && !f.RelativePath.Contains("\\dist\\", StringComparison.OrdinalIgnoreCase))
            .Where(f => f.SizeBytes <= settings.MaxFileSizeBytes)
            .Take(settings.MaxFilesPerAnalyzer);

    protected static async Task<string[]> SafeReadLinesAsync(IndexedFile file, CancellationToken cancellationToken)
        => File.Exists(file.Path) ? await File.ReadAllLinesAsync(file.Path, cancellationToken) : [];

    protected static async Task<string> SafeReadTextAsync(IndexedFile file, CancellationToken cancellationToken)
        => File.Exists(file.Path) ? await File.ReadAllTextAsync(file.Path, cancellationToken) : "";

    protected static bool IsGeneratedPath(string path)
        => path.StartsWith(".ai-context/", StringComparison.OrdinalIgnoreCase) ||
           path.StartsWith(".ai-context\\", StringComparison.OrdinalIgnoreCase) ||
           path.StartsWith("instructions/Result/", StringComparison.OrdinalIgnoreCase) ||
           path.StartsWith("instructions\\Result\\", StringComparison.OrdinalIgnoreCase) ||
           path.Contains("/bin/", StringComparison.OrdinalIgnoreCase) ||
           path.Contains("\\bin\\", StringComparison.OrdinalIgnoreCase) ||
           path.Contains("/obj/", StringComparison.OrdinalIgnoreCase) ||
           path.Contains("\\obj\\", StringComparison.OrdinalIgnoreCase);
}

public sealed class RazorBlazorAnalyzer : TextCodeAnalyzerBase
{
    public override CodeAnalyzerDescriptor Descriptor { get; } = new("razor-blazor", "Razor / Blazor Analyzer", CodeLanguage.Razor, AnalyzerState.Enabled, [AnalyzerCapability.Components, AnalyzerCapability.Routes, AnalyzerCapability.Imports], ["**/*.razor", "**/*.cshtml"]);
    public override bool CanAnalyze(ProjectScanResult scanResult) => scanResult.Files.Any(f => f.Extension is ".razor" or ".cshtml");
    protected override async Task FillAsync(ProjectScanResult scan, CodeAnalysisSettings settings, AnalyzerBuffer buffer, CancellationToken cancellationToken)
    {
        foreach (var file in CandidateFiles(scan, settings, ".razor", ".cshtml"))
        {
            buffer.FilesConsidered++;
            var lines = await SafeReadLinesAsync(file, cancellationToken);
            buffer.FilesAnalyzed++;
            var name = Path.GetFileNameWithoutExtension(file.RelativePath);
            var type = lines.Any(l => l.TrimStart().StartsWith("@page", StringComparison.OrdinalIgnoreCase)) ? ComponentType.BlazorPage : ComponentType.RazorComponent;
            buffer.Components.Add(new CodeComponent(Guid.NewGuid(), scan.Project.Id, scan.ScanRun.Id, file.RelativePath, name, type, CodeLanguage.Razor, "Blazor", 1, []));
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                var page = Regex.Match(line, "^@page\\s+\"(?<route>[^\"]+)\"");
                if (page.Success) buffer.FrontendRoutes.Add(new FrontendRoute(Guid.NewGuid(), scan.Project.Id, scan.ScanRun.Id, file.RelativePath, page.Groups["route"].Value, name, "Blazor", i + 1));
                var inject = Regex.Match(line, "^@inject\\s+(?<type>\\S+)\\s+(?<name>\\S+)");
                if (inject.Success) buffer.Imports.Add(new ScriptImport(Guid.NewGuid(), scan.Project.Id, scan.ScanRun.Id, file.RelativePath, inject.Groups["type"].Value, "RazorInject", i + 1));
            }
        }
    }
}

public sealed class MauiAnalyzer : TextCodeAnalyzerBase
{
    public override CodeAnalyzerDescriptor Descriptor { get; } = new("maui", "MAUI Analyzer", CodeLanguage.Xml, AnalyzerState.Enabled, [AnalyzerCapability.Components, AnalyzerCapability.Technologies], ["**/*.xaml", "**/*.csproj"]);
    public override bool CanAnalyze(ProjectScanResult scanResult) => scanResult.Files.Any(f => f.Extension.Equals(".xaml", StringComparison.OrdinalIgnoreCase)) || scanResult.Projects.Any(p => p.Sdk.Contains("Maui", StringComparison.OrdinalIgnoreCase) || p.Name.Contains("Maui", StringComparison.OrdinalIgnoreCase));
    protected override async Task FillAsync(ProjectScanResult scan, CodeAnalysisSettings settings, AnalyzerBuffer buffer, CancellationToken cancellationToken)
    {
        foreach (var file in CandidateFiles(scan, settings, ".xaml"))
        {
            buffer.FilesConsidered++;
            var text = await SafeReadTextAsync(file, cancellationToken);
            buffer.FilesAnalyzed++;
            if (text.Contains("ContentPage", StringComparison.OrdinalIgnoreCase) || text.Contains("BlazorWebView", StringComparison.OrdinalIgnoreCase))
                buffer.Components.Add(new CodeComponent(Guid.NewGuid(), scan.Project.Id, scan.ScanRun.Id, file.RelativePath, Path.GetFileNameWithoutExtension(file.RelativePath), ComponentType.MauiPage, CodeLanguage.Xml, "Maui", 1, text.Contains("BlazorWebView") ? ["BlazorWebView"] : []));
        }
    }
}

public sealed class JavaScriptAnalyzer : ScriptLikeAnalyzer
{
    public JavaScriptAnalyzer() : base("javascript", "JavaScript Analyzer", CodeLanguage.JavaScript, [".js", ".jsx", ".mjs", ".cjs"]) { }
}

public sealed class TypeScriptAnalyzer : ScriptLikeAnalyzer
{
    public TypeScriptAnalyzer() : base("typescript", "TypeScript Analyzer", CodeLanguage.TypeScript, [".ts", ".tsx"]) { }
}

public abstract class ScriptLikeAnalyzer(string id, string name, CodeLanguage language, string[] extensions) : TextCodeAnalyzerBase
{
    public override CodeAnalyzerDescriptor Descriptor { get; } = new(id, name, language, AnalyzerState.Enabled, [AnalyzerCapability.Symbols, AnalyzerCapability.Imports, AnalyzerCapability.Routes, AnalyzerCapability.Tests, AnalyzerCapability.Components], extensions.Select(e => $"**/*{e}").ToArray());
    public override bool CanAnalyze(ProjectScanResult scanResult) => scanResult.Files.Any(f => extensions.Contains(f.Extension, StringComparer.OrdinalIgnoreCase));
    protected override async Task FillAsync(ProjectScanResult scan, CodeAnalysisSettings settings, AnalyzerBuffer buffer, CancellationToken cancellationToken)
    {
        foreach (var file in CandidateFiles(scan, settings, extensions))
        {
            buffer.FilesConsidered++;
            var lines = await SafeReadLinesAsync(file, cancellationToken);
            buffer.FilesAnalyzed++;
            if (!settings.IncludeTests && IsTestPath(file.RelativePath)) continue;
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                foreach (Match m in Regex.Matches(line, "(?:import\\s+.*?\\s+from\\s+['\"](?<module>[^'\"]+)['\"]|require\\(['\"](?<module>[^'\"]+)['\"]\\))"))
                    buffer.Imports.Add(new ScriptImport(Guid.NewGuid(), scan.Project.Id, scan.ScanRun.Id, file.RelativePath, m.Groups["module"].Value, "Import", i + 1));
                var fn = Regex.Match(line, "(?:export\\s+)?(?:async\\s+)?function\\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)");
                if (fn.Success) buffer.Symbols.Add(Symbol(scan, file, fn.Groups["name"].Value, language == CodeLanguage.TypeScript ? "TypeScriptFunction" : "JavaScriptFunction", i + 1));
                var cls = Regex.Match(line, "(?:export\\s+)?class\\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)");
                if (cls.Success) buffer.Symbols.Add(Symbol(scan, file, cls.Groups["name"].Value, language == CodeLanguage.TypeScript ? "TypeScriptClass" : "JavaScriptClass", i + 1, CodeSymbolKind.Class));
                var route = Regex.Match(line, "\\.(?<method>get|post|put|delete|patch)\\s*\\(\\s*['\"](?<route>/[^'\"]*)['\"]");
                if (route.Success) buffer.Routes.Add(new RouteInfo(Guid.NewGuid(), scan.Project.Id, scan.ScanRun.Id, file.RelativePath, route.Groups["method"].Value.ToUpperInvariant(), route.Groups["route"].Value, null, i + 1));
                if (Regex.IsMatch(line, "\\b(describe|it|test)\\s*\\(")) buffer.Tests.Add(new TestInfo(Guid.NewGuid(), scan.Project.Id, scan.ScanRun.Id, file.RelativePath, Path.GetFileNameWithoutExtension(file.RelativePath), "script-test", "JS/TS", i + 1));
                var angular = Regex.Match(line, "selector\\s*:\\s*['\"](?<selector>[^'\"]+)['\"]");
                if (angular.Success) buffer.Components.Add(new CodeComponent(Guid.NewGuid(), scan.Project.Id, scan.ScanRun.Id, file.RelativePath, angular.Groups["selector"].Value, ComponentType.AngularComponent, language, "Angular", i + 1, []));
                var ngRoute = Regex.Match(line, "path\\s*:\\s*['\"](?<route>[^'\"]*)['\"]");
                if (ngRoute.Success && file.RelativePath.Contains("route", StringComparison.OrdinalIgnoreCase)) buffer.FrontendRoutes.Add(new FrontendRoute(Guid.NewGuid(), scan.Project.Id, scan.ScanRun.Id, file.RelativePath, ngRoute.Groups["route"].Value, null, "Angular", i + 1));
            }
        }
    }

    private static CodeSymbol Symbol(ProjectScanResult scan, IndexedFile file, string name, string ns, int line, CodeSymbolKind kind = CodeSymbolKind.Method)
        => new(Guid.NewGuid(), scan.Project.Id, scan.ScanRun.Id, file.Path, file.RelativePath, ns, name, name, kind, "exported-or-local", null, null, [], line, [], false, false);
    private static bool IsTestPath(string path) => path.Contains(".test.", StringComparison.OrdinalIgnoreCase) || path.Contains(".spec.", StringComparison.OrdinalIgnoreCase) || path.Contains("/test", StringComparison.OrdinalIgnoreCase) || path.Contains("\\test", StringComparison.OrdinalIgnoreCase);
}

public sealed class VueAnalyzer : TextCodeAnalyzerBase
{
    public override CodeAnalyzerDescriptor Descriptor { get; } = new("vue", "Vue Analyzer", CodeLanguage.Vue, AnalyzerState.Enabled, [AnalyzerCapability.Components, AnalyzerCapability.Imports, AnalyzerCapability.Styles], ["**/*.vue"]);
    public override bool CanAnalyze(ProjectScanResult scanResult) => scanResult.Files.Any(f => f.Extension.Equals(".vue", StringComparison.OrdinalIgnoreCase));
    protected override async Task FillAsync(ProjectScanResult scan, CodeAnalysisSettings settings, AnalyzerBuffer buffer, CancellationToken cancellationToken)
    {
        foreach (var file in CandidateFiles(scan, settings, ".vue"))
        {
            buffer.FilesConsidered++;
            var lines = await SafeReadLinesAsync(file, cancellationToken);
            buffer.FilesAnalyzed++;
            buffer.Components.Add(new CodeComponent(Guid.NewGuid(), scan.Project.Id, scan.ScanRun.Id, file.RelativePath, Path.GetFileNameWithoutExtension(file.RelativePath), ComponentType.VueComponent, CodeLanguage.Vue, "Vue", 1, []));
            for (var i = 0; i < lines.Length; i++)
            {
                foreach (Match m in Regex.Matches(lines[i], "import\\s+.*?\\s+from\\s+['\"](?<module>[^'\"]+)['\"]"))
                    buffer.Imports.Add(new ScriptImport(Guid.NewGuid(), scan.Project.Id, scan.ScanRun.Id, file.RelativePath, m.Groups["module"].Value, "Import", i + 1));
                foreach (Match m in Regex.Matches(lines[i], "class\\s*=\\s*['\"](?<class>[^'\"]+)['\"]"))
                    buffer.Styles.Add(new StyleUsage(Guid.NewGuid(), scan.Project.Id, scan.ScanRun.Id, file.RelativePath, m.Groups["class"].Value, "ClassAttribute", i + 1));
            }
        }
    }
}

public sealed class HtmlAnalyzer : TextCodeAnalyzerBase
{
    public override CodeAnalyzerDescriptor Descriptor { get; } = new("html", "HTML Analyzer", CodeLanguage.Html, AnalyzerState.Enabled, [AnalyzerCapability.Components, AnalyzerCapability.Imports, AnalyzerCapability.Styles], ["**/*.html"]);
    public override bool CanAnalyze(ProjectScanResult scanResult) => scanResult.Files.Any(f => f.Extension.Equals(".html", StringComparison.OrdinalIgnoreCase));
    protected override async Task FillAsync(ProjectScanResult scan, CodeAnalysisSettings settings, AnalyzerBuffer buffer, CancellationToken cancellationToken)
    {
        foreach (var file in CandidateFiles(scan, settings, ".html", ".htm"))
        {
            buffer.FilesConsidered++;
            var lines = await SafeReadLinesAsync(file, cancellationToken);
            buffer.FilesAnalyzed++;
            buffer.Components.Add(new CodeComponent(Guid.NewGuid(), scan.Project.Id, scan.ScanRun.Id, file.RelativePath, Path.GetFileNameWithoutExtension(file.RelativePath), ComponentType.HtmlPage, CodeLanguage.Html, null, 1, []));
            for (var i = 0; i < lines.Length; i++)
            {
                foreach (Match m in Regex.Matches(lines[i], "<script[^>]+src=['\"](?<src>[^'\"]+)['\"]")) buffer.Imports.Add(new ScriptImport(Guid.NewGuid(), scan.Project.Id, scan.ScanRun.Id, file.RelativePath, m.Groups["src"].Value, "ScriptSrc", i + 1));
                foreach (Match m in Regex.Matches(lines[i], "class\\s*=\\s*['\"](?<class>[^'\"]+)['\"]")) buffer.Styles.Add(new StyleUsage(Guid.NewGuid(), scan.Project.Id, scan.ScanRun.Id, file.RelativePath, m.Groups["class"].Value, "ClassAttribute", i + 1));
            }
        }
    }
}

public sealed class CssAnalyzer : TextCodeAnalyzerBase
{
    public override CodeAnalyzerDescriptor Descriptor { get; } = new("css", "CSS Analyzer", CodeLanguage.Css, AnalyzerState.Enabled, [AnalyzerCapability.Styles], ["**/*.css", "**/*.scss", "**/*.sass", "**/*.less"]);
    public override bool CanAnalyze(ProjectScanResult scanResult) => scanResult.Files.Any(f => f.Extension is ".css" or ".scss" or ".sass" or ".less");
    protected override async Task FillAsync(ProjectScanResult scan, CodeAnalysisSettings settings, AnalyzerBuffer buffer, CancellationToken cancellationToken)
    {
        foreach (var file in CandidateFiles(scan, settings, ".css", ".scss", ".sass", ".less"))
        {
            buffer.FilesConsidered++;
            var lines = await SafeReadLinesAsync(file, cancellationToken);
            buffer.FilesAnalyzed++;
            for (var i = 0; i < lines.Length; i++)
            {
                var m = Regex.Match(lines[i], @"(?<selector>[.#][A-Za-z0-9_-]+|:root)\s*\{");
                if (m.Success) buffer.Styles.Add(new StyleUsage(Guid.NewGuid(), scan.Project.Id, scan.ScanRun.Id, file.RelativePath, m.Groups["selector"].Value, "Selector", i + 1));
            }
        }
    }
}

public sealed class NodePackageAnalyzer : TextCodeAnalyzerBase
{
    public override CodeAnalyzerDescriptor Descriptor { get; } = new("node-package", "Node / package.json Analyzer", CodeLanguage.Json, AnalyzerState.Enabled, [AnalyzerCapability.Packages, AnalyzerCapability.BuildScripts, AnalyzerCapability.Technologies], ["package.json"]);
    public override bool CanAnalyze(ProjectScanResult scanResult) => scanResult.Files.Any(f => f.RelativePath.EndsWith("package.json", StringComparison.OrdinalIgnoreCase));
    protected override async Task FillAsync(ProjectScanResult scan, CodeAnalysisSettings settings, AnalyzerBuffer buffer, CancellationToken cancellationToken)
    {
        foreach (var file in CandidateFiles(scan, settings, "package.json"))
        {
            buffer.FilesConsidered++;
            var text = await SafeReadTextAsync(file, cancellationToken);
            buffer.FilesAnalyzed++;
            using var doc = JsonDocument.Parse(text);
            AddPackages(scan, buffer, file, doc.RootElement, "dependencies", "runtime");
            AddPackages(scan, buffer, file, doc.RootElement, "devDependencies", "dev");
            if (doc.RootElement.TryGetProperty("scripts", out var scripts) && scripts.ValueKind == JsonValueKind.Object)
            {
                foreach (var script in scripts.EnumerateObject())
                    buffer.BuildScripts.Add(new BuildScriptInfo(Guid.NewGuid(), scan.Project.Id, scan.ScanRun.Id, file.RelativePath, script.Name, script.Value.GetString() ?? "", PackageEcosystem.Npm));
            }
        }
    }

    private static void AddPackages(ProjectScanResult scan, AnalyzerBuffer buffer, IndexedFile file, JsonElement root, string property, string scope)
    {
        if (!root.TryGetProperty(property, out var deps) || deps.ValueKind != JsonValueKind.Object) return;
        foreach (var dep in deps.EnumerateObject())
            buffer.PackageReferences.Add(new PackageReferenceInfo(Guid.NewGuid(), scan.Project.Id, scan.ScanRun.Id, file.RelativePath, dep.Name, dep.Value.GetString(), PackageEcosystem.Npm, scope));
    }
}

public sealed class ConfigAnalyzer : TextCodeAnalyzerBase
{
    public override CodeAnalyzerDescriptor Descriptor { get; } = new("config", "JSON / YAML Config Analyzer", CodeLanguage.Json, AnalyzerState.Enabled, [AnalyzerCapability.Configuration], ["**/*.json", "**/*.yaml", "**/*.yml"]);
    public override bool CanAnalyze(ProjectScanResult scanResult) => scanResult.Files.Any(f => f.Extension is ".json" or ".yaml" or ".yml");
    protected override async Task FillAsync(ProjectScanResult scan, CodeAnalysisSettings settings, AnalyzerBuffer buffer, CancellationToken cancellationToken)
    {
        foreach (var file in CandidateFiles(scan, settings, ".json", ".yaml", ".yml"))
        {
            if (file.RelativePath.EndsWith("package.json", StringComparison.OrdinalIgnoreCase)) continue;
            buffer.FilesConsidered++;
            var lines = await SafeReadLinesAsync(file, cancellationToken);
            buffer.FilesAnalyzed++;
            for (var i = 0; i < Math.Min(lines.Length, 200); i++)
            {
                var key = Regex.Match(lines[i], "['\"]?(?<key>[A-Za-z0-9_.:-]+)['\"]?\\s*:");
                if (key.Success) buffer.ConfigurationUsages.Add(new ConfigurationUsage(Guid.NewGuid(), scan.Project.Id, scan.ScanRun.Id, file.RelativePath, key.Groups["key"].Value, "ConfigKey", i + 1));
            }
        }
    }
}

public sealed class DockerAnalyzer : TextCodeAnalyzerBase
{
    public override CodeAnalyzerDescriptor Descriptor { get; } = new("docker", "Docker Analyzer", CodeLanguage.Dockerfile, AnalyzerState.Enabled, [AnalyzerCapability.BuildScripts, AnalyzerCapability.Technologies], ["Dockerfile", "**/*.dockerfile"]);
    public override bool CanAnalyze(ProjectScanResult scanResult) => scanResult.Files.Any(f => f.RelativePath.EndsWith("Dockerfile", StringComparison.OrdinalIgnoreCase) || f.Extension.Equals(".dockerfile", StringComparison.OrdinalIgnoreCase));
    protected override async Task FillAsync(ProjectScanResult scan, CodeAnalysisSettings settings, AnalyzerBuffer buffer, CancellationToken cancellationToken)
    {
        foreach (var file in CandidateFiles(scan, settings, "Dockerfile", ".dockerfile"))
        {
            buffer.FilesConsidered++;
            var lines = await SafeReadLinesAsync(file, cancellationToken);
            buffer.FilesAnalyzed++;
            foreach (var line in lines.Select(l => l.Trim()).Where(l => l.StartsWith("FROM ", StringComparison.OrdinalIgnoreCase)))
                buffer.BuildScripts.Add(new BuildScriptInfo(Guid.NewGuid(), scan.Project.Id, scan.ScanRun.Id, file.RelativePath, "Docker base image", line, PackageEcosystem.Docker));
        }
    }
}

public sealed class GitHubActionsAnalyzer : TextCodeAnalyzerBase
{
    public override CodeAnalyzerDescriptor Descriptor { get; } = new("github-actions", "GitHub Actions Analyzer", CodeLanguage.GitHubActions, AnalyzerState.Enabled, [AnalyzerCapability.BuildScripts], [".github/workflows/*.yml", ".github/workflows/*.yaml"]);
    public override bool CanAnalyze(ProjectScanResult scanResult) => scanResult.Files.Any(f => f.RelativePath.StartsWith(".github/workflows/", StringComparison.OrdinalIgnoreCase));
    protected override async Task FillAsync(ProjectScanResult scan, CodeAnalysisSettings settings, AnalyzerBuffer buffer, CancellationToken cancellationToken)
    {
        foreach (var file in CandidateFiles(scan, settings, ".yaml", ".yml").Where(f => f.RelativePath.StartsWith(".github/workflows/", StringComparison.OrdinalIgnoreCase)))
        {
            buffer.FilesConsidered++;
            var lines = await SafeReadLinesAsync(file, cancellationToken);
            buffer.FilesAnalyzed++;
            foreach (var run in lines.Select((line, index) => (line: line.Trim(), index)).Where(x => x.line.StartsWith("run:", StringComparison.OrdinalIgnoreCase)))
                buffer.BuildScripts.Add(new BuildScriptInfo(Guid.NewGuid(), scan.Project.Id, scan.ScanRun.Id, file.RelativePath, $"workflow:{run.index + 1}", run.line[4..].Trim(), PackageEcosystem.GitHubActions));
        }
    }
}

public sealed class AnalyzerBuffer(ProjectScanResult scan, CodeAnalyzerDescriptor descriptor)
{
    public int FilesConsidered { get; set; }
    public int FilesAnalyzed { get; set; }
    public List<string> Warnings { get; } = [];
    public List<CodeSymbol> Symbols { get; } = [];
    public List<RouteInfo> Routes { get; } = [];
    public List<ServiceRegistration> ServiceRegistrations { get; } = [];
    public List<ConfigurationUsage> ConfigurationUsages { get; } = [];
    public List<BackgroundWorkerInfo> BackgroundWorkers { get; } = [];
    public List<TestInfo> Tests { get; } = [];
    public List<CodeFlow> Flows { get; } = [];
    public List<CodeComponent> Components { get; } = [];
    public List<FrontendRoute> FrontendRoutes { get; } = [];
    public List<StyleUsage> Styles { get; } = [];
    public List<ScriptImport> Imports { get; } = [];
    public List<PackageReferenceInfo> PackageReferences { get; } = [];
    public List<BuildScriptInfo> BuildScripts { get; } = [];
    public List<DetectedTechnology> Technologies { get; } = [];
    public int ItemCount => Symbols.Count + Routes.Count + ServiceRegistrations.Count + ConfigurationUsages.Count + BackgroundWorkers.Count + Tests.Count + Flows.Count + Components.Count + FrontendRoutes.Count + Styles.Count + Imports.Count + PackageReferences.Count + BuildScripts.Count;

    public CodeAnalyzerResult ToResult(CodeAnalyzerRun run)
        => new(descriptor, Symbols, Routes, ServiceRegistrations, ConfigurationUsages, BackgroundWorkers, Tests, Flows, Components, FrontendRoutes, Styles, Imports, PackageReferences, BuildScripts, Technologies, run);
}

internal static class AnalyzerResultFactory
{
    public static CodeAnalyzerResult Empty(CodeAnalyzerDescriptor descriptor, CodeAnalyzerRun run)
        => new(descriptor, [], [], [], [], [], [], [], [], [], [], [], [], [], [], run);

    public static CodeAnalyzerRun Run(ProjectScanResult scan, CodeAnalyzerDescriptor descriptor, AnalyzerState state, int filesConsidered, int filesAnalyzed, int itemsDetected, TimeSpan duration, IReadOnlyList<string> warnings, string? error, DateTimeOffset started)
        => new(Guid.NewGuid(), scan.Project.Id, scan.ScanRun.Id, descriptor.Id, descriptor.Language, state, filesConsidered, filesAnalyzed, itemsDetected, duration, warnings, error, started, DateTimeOffset.UtcNow);
}
