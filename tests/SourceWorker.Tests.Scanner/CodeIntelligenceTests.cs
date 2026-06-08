using SourceWorker.Application;
using SourceWorker.Domain;
using SourceWorker.Scanner;

namespace SourceWorker.Tests.Scanner;

public sealed class CodeIntelligenceTests
{
    [Fact]
    public async Task CodeAnalyzer_pipeline_detects_frontend_packages_components_imports_and_styles()
    {
        var root = Path.Combine(Path.GetTempPath(), "sourceworker-multilang-" + Guid.NewGuid());
        Directory.CreateDirectory(Path.Combine(root, "src", "components"));
        Directory.CreateDirectory(Path.Combine(root, ".github", "workflows"));
        await File.WriteAllTextAsync(Path.Combine(root, "Sample.cs"), "namespace Sample; public class ApiService { public void Run() { } }");
        await File.WriteAllTextAsync(Path.Combine(root, "src", "App.razor"), """
            @page "/dashboard"
            @inject ISourceWorkerApiClient Api
            <h1>Dashboard</h1>
            """);
        await File.WriteAllTextAsync(Path.Combine(root, "src", "components", "Widget.vue"), """
            <template><section class="widget"><button @click="save">Save</button></section></template>
            <script setup>
            import helper from './helper'
            const props = defineProps({ title: String })
            </script>
            <style scoped>.widget { color: red; }</style>
            """);
        await File.WriteAllTextAsync(Path.Combine(root, "src", "main.ts"), "import { createApp } from 'vue'; export function boot() { return createApp({}); }");
        await File.WriteAllTextAsync(Path.Combine(root, "src", "styles.css"), ":root { --brand: teal; }\n.app-shell { display: grid; }");
        await File.WriteAllTextAsync(Path.Combine(root, "package.json"), """
            {
              "dependencies": { "vue": "latest", "vite": "latest" },
              "devDependencies": { "typescript": "latest" },
              "scripts": { "dev": "vite", "build": "vite build" }
            }
            """);
        await File.WriteAllTextAsync(Path.Combine(root, "Dockerfile"), "FROM mcr.microsoft.com/dotnet/runtime:10.0");
        await File.WriteAllTextAsync(Path.Combine(root, ".github", "workflows", "build.yml"), "name: build\njobs:\n  build:\n    steps:\n      - run: dotnet build");

        var project = new Project(Guid.NewGuid(), "MultiLang", root, DateTimeOffset.UtcNow);
        var scan = await new BasicProjectScanner(new EmptyGitInspector()).ScanAsync(project, CancellationToken.None);
        var pipeline = CreatePipeline();
        var result = await pipeline.AnalyzeAsync(scan, CreateDefaultSettings(project.Id), CancellationToken.None);

        Assert.Contains(result.LanguageProfile!.Languages, l => l == CodeLanguage.CSharp);
        Assert.Contains(result.LanguageProfile.Languages, l => l == CodeLanguage.Razor);
        Assert.Contains(result.LanguageProfile.Languages, l => l == CodeLanguage.Vue);
        Assert.Contains(result.LanguageProfile.Frameworks, f => f == CodeFramework.Vue);
        Assert.Contains(result.LanguageProfile.Frameworks, f => f == CodeFramework.Vite);
        Assert.Contains(result.Components, c => c.Name == "App" && c.Type == ComponentType.BlazorPage);
        Assert.Contains(result.Components, c => c.Name == "Widget" && c.Type == ComponentType.VueComponent);
        Assert.Contains(result.FrontendRoutes, r => r.RoutePattern == "/dashboard");
        Assert.Contains(result.Imports, i => i.Module.Contains("vue", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Styles, s => s.SelectorOrClass.Contains("app-shell", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.PackageReferences, p => p.Name == "vue");
        Assert.Contains(result.BuildScripts, s => s.Name == "build");
        Assert.Contains(result.BuildScripts, s => s.Ecosystem == PackageEcosystem.Docker);
        Assert.Contains(result.AnalyzerRuns, r => r.AnalyzerId == "vue" && r.ItemsDetected > 0);
    }

    [Fact]
    public async Task CodeAnalyzer_pipeline_respects_disabled_analyzer_settings()
    {
        var root = Path.Combine(Path.GetTempPath(), "sourceworker-disabled-analyzer-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(Path.Combine(root, "App.vue"), "<template><main class=\"app\"></main></template><script setup>defineProps({ title: String })</script>");

        var project = new Project(Guid.NewGuid(), "DisabledAnalyzer", root, DateTimeOffset.UtcNow);
        var scan = await new BasicProjectScanner(new EmptyGitInspector()).ScanAsync(project, CancellationToken.None);
        var settings = CreateDefaultSettings(project.Id) with
        {
            AnalyzerEnabled = CreateAnalyzerMap(["vue"], enabled: false)
        };

        var result = await CreatePipeline().AnalyzeAsync(scan, settings, CancellationToken.None);

        Assert.DoesNotContain(result.Components, c => c.Type == ComponentType.VueComponent);
        Assert.DoesNotContain(result.AnalyzerRuns, r => r.AnalyzerId == "vue");
    }

    [Fact]
    public async Task Detects_symbols_routes_services_config_workers_tests_and_flows()
    {
        var root = Path.Combine(Path.GetTempPath(), "sourceworker-code-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(Path.Combine(root, "Program.cs"), """
            namespace Sample;
            public interface IFoo { }
            public class Worker : BackgroundService { public async Task RunAsync() { } }
            public class HomeController {
              [HttpGet("/home")]
              public async Task<string> Get() { return ""; }
            }
            public class Startup {
              public void Configure() {
                app.MapGet("/health", () => "ok");
                services.AddSingleton<IFoo, Foo>();
                Configuration.GetSection("App");
              }
            }
            public class Tests {
              [Fact]
              public void Works() { }
            }
            """);
        var project = new Project(Guid.NewGuid(), "Sample", root, DateTimeOffset.UtcNow);
        var scan = await new BasicProjectScanner(new EmptyGitInspector()).ScanAsync(project, CancellationToken.None);
        var result = await new CSharpCodeIntelligenceScanner(new RuleBasedFlowExtractor()).ScanAsync(scan, CancellationToken.None);

        Assert.Contains(result.Symbols, s => s.Kind == CodeSymbolKind.Class && s.Name == "Worker");
        Assert.Contains(result.Symbols, s => s.Kind == CodeSymbolKind.Interface && s.Name == "IFoo");
        Assert.Contains(result.Symbols, s => s.Kind == CodeSymbolKind.Method && s.Name == "Get" && s.IsAsync);
        Assert.Contains(result.Routes, r => r.RoutePattern == "/health");
        Assert.Contains(result.Routes, r => r.RoutePattern == "/home");
        Assert.Contains(result.ServiceRegistrations, s => s.ServiceType == "IFoo");
        Assert.Contains(result.ConfigurationUsages, c => c.KeyOrSection == "App");
        Assert.Contains(result.BackgroundWorkers, w => w.ClassName == "Worker");
        Assert.Contains(result.Tests, t => t.MethodName == "Works");
        Assert.Contains(result.Flows, f => f.FlowType == CodeFlowType.LocalApiEndpointFlow && f.Confidence == FlowConfidence.High);
        Assert.DoesNotContain(result.Flows, f => f.Origin == FlowOrigin.SourceWorkerInternal);
        Assert.Contains("flowchart TD", new FlowDiagramGenerator().Generate(result.Flows.First()));
    }

    private sealed class EmptyGitInspector : IGitInspector
    {
        public Task<GitSnapshot> InspectAsync(Project project, CancellationToken cancellationToken)
            => Task.FromResult(new GitSnapshot(Guid.NewGuid(), project.Id, null, null, false, DateTimeOffset.UtcNow));
    }

    private static CodeIntelligencePipeline CreatePipeline()
    {
        ICodeAnalyzer[] analyzers =
        [
            new CSharpCodeAnalyzer(new CSharpCodeIntelligenceScanner(new RuleBasedFlowExtractor())),
            new RazorBlazorAnalyzer(),
            new MauiAnalyzer(),
            new JavaScriptAnalyzer(),
            new TypeScriptAnalyzer(),
            new VueAnalyzer(),
            new HtmlAnalyzer(),
            new CssAnalyzer(),
            new NodePackageAnalyzer(),
            new ConfigAnalyzer(),
            new DockerAnalyzer(),
            new GitHubActionsAnalyzer()
        ];
        return new CodeIntelligencePipeline(new CodeAnalyzerRegistry(analyzers), new LanguageDetectionService());
    }

    private static CodeAnalysisSettings CreateDefaultSettings(Guid projectId)
        => new(projectId, CreateAnalyzerMap([
            "csharp",
            "razor-blazor",
            "maui",
            "javascript",
            "typescript",
            "vue",
            "html",
            "css",
            "node-package",
            "config",
            "docker",
            "github-actions"
        ]));

    private static IReadOnlyDictionary<string, bool> CreateAnalyzerMap(IEnumerable<string> analyzerIds, bool enabled = true)
        => analyzerIds.ToDictionary(id => id, _ => enabled, StringComparer.OrdinalIgnoreCase);
}
