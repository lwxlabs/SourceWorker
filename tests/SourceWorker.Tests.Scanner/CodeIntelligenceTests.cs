using SourceWorker.Application;
using SourceWorker.Domain;
using SourceWorker.Scanner;

namespace SourceWorker.Tests.Scanner;

public sealed class CodeIntelligenceTests
{
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
}
